using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Infrastructure.Shipping;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #20] Goship is asked for the pickup address saved in the Farmer profile")]
    public async Task Quote_UsesFarmerProfilePickupAddress()
    {
        await using (var db = _api.Open())
        {
            var farm = await db.FarmerProfiles.SingleAsync(x => x.UserId == _api.FarmerUserId);
            (farm.PickupProvinceCode, farm.PickupDistrictCode, farm.PickupWardCode) = ("79", "760", "26734");
            await db.SaveChangesAsync();
        }
        var goship = new RecordingGoship();
        _api.Shipping.Through = goship.Provider();

        await ApiAssert.Status(await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput()),
            HttpStatusCode.OK);

        using var body = JsonDocument.Parse(Assert.Single(goship.Bodies));
        var from = body.RootElement.GetProperty("shipment").GetProperty("address_from");
        Assert.Equal(("79", "760"), (from.GetProperty("city").GetString(), from.GetProperty("district").GetString()));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #21] A farm without a pickup address gets no delivery fee (MSG-CO-02)")]
    public async Task Quote_FarmWithoutPickup_ShippingUnavailable()
    {
        var goship = new RecordingGoship();
        _api.Shipping.Through = goship.Provider();

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput());

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "SHIPPING_RATE_UNAVAILABLE");
        Assert.Empty(goship.Bodies);
        await using var db = _api.Open();
        Assert.Single(await db.CartItems.ToListAsync());
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #22] PayOS is no longer an accepted payment method")]
    public async Task Quote_PayOs_FieldError()
    {
        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes",
            ApiHost.CheckoutInput() with { PaymentMethod = "PayOS" });

        ApiAssert.HasFieldError(await ApiAssert.Status(response, HttpStatusCode.BadRequest), "PaymentMethod");
        await using var db = _api.Open();
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #23] Points above 50 % of the subtotal are rejected with the maximum (MSG-CO-03)")]
    public async Task Quote_PointsAboveCap_RejectedWithMaximum()
    {
        await using (var db = _api.Open())
        {
            (await db.Products.SingleAsync(x => x.ProductId == _api.SpinachId)).BaseRetailPrice = 40_000;
            db.RewardPointEntries.Add(new AgriSustain.Domain.Entities.RewardPointEntry
            {
                UserId = _api.BuyerId, EntryType = "Earn", Points = 99_000, RequestKey = "earn-cap",
                OccurredAt = ApiHost.T0.UtcDateTime
            });
            await db.SaveChangesAsync();
        }

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput(points: 50_000));

        var problem = await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "POINTS_INVALID");
        Assert.Contains("tối đa 40000 điểm", problem.GetProperty("title").GetString());
        await using var check = _api.Open();
        Assert.Empty(await check.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #24] Points above the balance are rejected")]
    public async Task Quote_PointsAboveBalance_Rejected()
    {
        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/quotes", ApiHost.CheckoutInput(points: 1_500));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "POINTS_INVALID");
        await using var db = _api.Open();
        Assert.Empty(await db.RetailCheckoutQuotes.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-02 #25] Voucher deactivated after the quote: 409 MSG118, no order, cart kept")]
    public async Task Place_VoucherDeactivatedAfterQuote_Msg118()
    {
        await using (var db = _api.Open())
        {
            db.Vouchers.Add(new AgriSustain.Domain.Entities.Voucher
            {
                VoucherCode = "SALE10", VoucherName = "Sale 10", DiscountType = "Percentage", DiscountValue = 10,
                MinOrderAmount = 0, UsageLimit = 100, PerCustomerLimit = 1, IsActive = true,
                StartDate = ApiHost.T0.UtcDateTime.AddDays(-1), EndDate = ApiHost.T0.UtcDateTime.AddDays(10)
            });
            await db.SaveChangesAsync();
        }
        var quote = await _api.QuoteAsync(ApiHost.CheckoutInput(voucher: "SALE10"));
        Assert.True(quote.VoucherDiscount > 0);
        await using (var db = _api.Open())
        {
            (await db.Vouchers.SingleAsync(x => x.VoucherCode == "SALE10")).IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await _api.Buyer().PostAsJsonAsync("api/checkout/orders", Place(quote.Id));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "MSG118");
        await using var check = _api.Open();
        Assert.Empty(await check.Orders.Where(order => order.BuyerUserId == _api.BuyerId).ToListAsync());
        Assert.Empty(await check.VoucherRedemptions.ToListAsync());
        Assert.Single(await check.CartItems.ToListAsync());
    }

    /// <summary>The real Goship adapter on a stub HTTP handler that records each POST /rates body.</summary>
    private sealed class RecordingGoship : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        public GoshipShippingRateProvider Provider()
        {
            var http = new HttpClient(this) { BaseAddress = new Uri("https://sandbox.goship.io/api/v2/") };
            return new GoshipShippingRateProvider(new GoshipClient(http),
                Options.Create(new GoshipOptions { Enabled = true, AccessToken = "test-token" }));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"code":200,"status":"success","data":[{"id":"a","carrier_short_name":"ghtk","service":"Chậm","total_fee":19540}]}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
