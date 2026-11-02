using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>UC-RC-02 through CheckoutController on SQL Server.</summary>
[Trait("Layer", "SqlIntegration")]
public sealed class CheckoutApiTests : IAsyncLifetime
{
    private ApiHost _api = null!;

    public async Task InitializeAsync() => _api = await ApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static PlaceOrderRequest Place(Guid quoteId, string? key = null, bool confirmed = true) =>
        new(quoteId, key ?? Guid.NewGuid().ToString("D"), confirmed);

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #12] Farmer cannot request a checkout quote")]
    public async Task Quote_Farmer_Forbidden()
    {
        var response = await _api.As(_api.FarmerUserId, "Farmer").PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput());

        await ApiAssert.Status(response, HttpStatusCode.Forbidden);
        await using var db = _api.Open();
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #13] Invalid phone is a field error and saves no quote")]
    public async Task Quote_InvalidPhone_FieldError()
    {
        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput(phone: "0401234567"));

        ApiAssert.HasFieldError(await ApiAssert.Status(response, HttpStatusCode.BadRequest), "Address.Phone");
        await using var db = _api.Open();
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #14] Placement without confirmation keeps the quote and creates no order")]
    public async Task Place_Unconfirmed_Validation()
    {
        var quote = await _api.QuoteAsync();

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(quote.Id, confirmed: false));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "VALIDATION");
        await using var db = _api.Open();
        Assert.Single(await db.RetailCheckoutQuotes.ToListAsync());
        Assert.Empty(await db.Orders.Where(order => order.BuyerUserId == _api.BuyerId).ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #15] Quote is expired exactly at its expiry time")]
    public async Task Place_AtExpiry_Conflict()
    {
        var quote = await _api.QuoteAsync();
        _api.Clock.Advance(TimeSpan.FromMinutes(5));

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(quote.Id));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "QUOTE_EXPIRED");
        await using var db = _api.Open();
        Assert.Equal(0m, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #16] Request key reused for another quote conflicts")]
    public async Task Place_KeyReusedForOtherQuote_Conflict()
    {
        var first = await _api.QuoteAsync();
        var second = await _api.QuoteAsync();
        string key = Guid.NewGuid().ToString("D");

        await ApiAssert.Status(await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(first.Id, key)),
            HttpStatusCode.Created);
        var reused = await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(second.Id, key));

        await ApiAssert.Problem(reused, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT");
        await using var db = _api.Open();
        Assert.Single(await db.Orders.Where(order => order.BuyerUserId == _api.BuyerId).ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #17] Another buyer cannot read the order")]
    public async Task Order_OtherBuyer_NotFound()
    {
        var quote = await _api.QuoteAsync();
        var placed = await ApiAssert.Status(await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(quote.Id)),
            HttpStatusCode.Created);
        long orderId = placed.GetProperty("id").GetInt64();

        var response = await _api.As(_api.OtherBuyerId, "RetailCustomer").GetAsync($"api/checkout/orders/{orderId}");

        await ApiAssert.Problem(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #18] Shipping rate service down leaves the cart unchanged")]
    public async Task Quote_ShippingDown_Unavailable()
    {
        _api.Shipping.Down = true;

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput());

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "SHIPPING_RATE_UNAVAILABLE");
        await using var db = _api.Open();
        Assert.Single(await db.CartItems.ToListAsync());
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }
}
