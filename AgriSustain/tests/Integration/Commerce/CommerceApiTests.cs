using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgriSustain.Api.Controllers;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.UnitTests.Commerce;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

// Component integration: real routing, auth policy, binding, controllers, services,
// validators, pricing and exception middleware. Persistence ports are test doubles.
// SQL/transaction guarantees are deliberately covered by CommerceSqlTests instead.
[Trait("Layer", "ApiIntegration")]
public sealed partial class CommerceApiTests
{
    private sealed class Api : IAsyncDisposable
    {
        public readonly CommerceFixture Data = new();
        private WebApplication host = null!;
        public HttpClient Client = null!;
        public static async Task<Api> Create(long? user = 101, string role = "Farmer")
        {
            var f = new Api();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
            builder.Services.AddSingleton(f.Data.FarmerProductService);
            builder.Services.AddSingleton(f.Data.RetailCartService);
            builder.Services.AddSingleton(f.Data.OrderService);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentActor, CurrentActor>();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(FarmerProductController).Assembly);
            f.host = builder.Build();
            f.host.UseMiddleware<FeatureExceptionMiddleware>();
            f.host.UseAuthentication(); f.host.UseAuthorization(); f.host.MapControllers();
            await f.host.StartAsync(); f.Client = f.host.GetTestClient();
            if (user.HasValue) f.Client.DefaultRequestHeaders.Add("X-Test-User", $"{user}:{role}");
            return f;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await host.DisposeAsync(); }
    }
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotMatch(@"MSG\d+", body);
        if (code is not null)
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        }
    }

    [Theory]
    [InlineData("api/farmer/products"), InlineData("api/farmer/orders"), InlineData("api/cart")]
    public async Task AnonymousRequest_IsChallengedBeforeService(string path)
    {
        await using var api = await Api.Create(null);
        await Error(await api.Client.GetAsync(path), HttpStatusCode.Unauthorized);
        Assert.Equal(0, api.Data.Work.Begins);
    }
    [Theory]
    [InlineData("api/farmer/products", "RetailCustomer"), InlineData("api/farmer/orders", "RetailCustomer")]
    [InlineData("api/cart", "Farmer")]
    public async Task WrongRole_IsForbiddenBeforeService(string path, string role)
    {
        await using var api = await Api.Create(101, role);
        await Error(await api.Client.GetAsync(path), HttpStatusCode.Forbidden);
        Assert.Equal(0, api.Data.Work.Begins);
    }
    [Fact, Trait("UseCase", "UC-FAR-03")]
    public async Task ProductSubmission_ReturnsCreatedWithLocationAndPendingVietnameseContract()
    {
        await using var api = await Api.Create();
        var response = await api.Client.PostAsJsonAsync("api/farmer/products", api.Data.Request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/farmer/products/302", response.Headers.Location!.ToString());
        var value = await Read<ProductResponse>(response);
        Assert.Equal("Pending", value.ApprovalStatus); Assert.False(value.IsPublished);
        Assert.Equal(1, api.Data.Work.Commits);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("originProvince", text); Assert.DoesNotContain("originCity", text);
    }
    [Theory, Trait("UseCase", "UC-FAR-03")]
    [InlineData("name"), InlineData("unit"), InlineData("description"), InlineData("price"), InlineData("wholesale"), InlineData("images")]
    public async Task ProductValidation_ReturnsFieldErrorsWithoutMessageCodes(string field)
    {
        await using var api = await Api.Create();
        var request = field switch
        {
            "name" => api.Data.Request with { ProductName = "" },
            "unit" => api.Data.Request with { Unit = "" },
            "description" => api.Data.Request with { Description = "" },
            "price" => api.Data.Request with { BaseRetailPrice = 0 },
            "wholesale" => api.Data.Request with { WholesalePrice = 30001 },
            _ => api.Data.Request with { Images = [] }
        };
        var response = await api.Client.PostAsJsonAsync("api/farmer/products", request);
        await Error(response, HttpStatusCode.BadRequest, "VALIDATION");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEmpty(json.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal(0, api.Data.Work.Commits);
    }
    [Fact, Trait("UseCase", "UC-FAR-03")]
    public async Task ProductStaleVersion_Returns409()
    {
        await using var api = await Api.Create();
        await Error(await api.Client.PutAsJsonAsync("api/farmer/products/301",
            api.Data.Request with { Version = new string('B', 64) }), HttpStatusCode.Conflict, "CONFLICT");
        Assert.Equal(0, api.Data.Work.Commits);
    }
    [Fact, Trait("UseCase", "UC-FAR-03")]
    public async Task VisibilityRequest_BindsDto_AndHidesOwnedProduct()
    {
        await using var api = await Api.Create();
        var result = await Read<ProductResponse>(await api.Client.PutAsJsonAsync("api/farmer/products/301/visibility",
            new VisibilityRequest(false, ProductDouble.Version)));
        Assert.False(result.IsPublished); Assert.Equal("Approved", result.ApprovalStatus);
    }
    [Theory, Trait("UseCase", "UC-FAR-03")]
    [InlineData(false), InlineData(true)]
    public async Task ProductStorageFailure_ReturnsFriendlyProblemDetails(bool visibility)
    {
        await using var api = await Api.Create();
        // Exercise both product creation and visibility commits.
        api.Data.Work.FailCommit = true;
        var response = visibility
            ? await api.Client.PutAsJsonAsync("api/farmer/products/301/visibility", new VisibilityRequest(false, ProductDouble.Version))
            : await api.Client.PostAsJsonAsync("api/farmer/products", api.Data.Request);
        await Error(response, HttpStatusCode.InternalServerError, "SERVER_ERROR");
        Assert.Equal(0, api.Data.Work.Commits);
    }
    [Theory, Trait("UseCase", "UC-RC-01")]
    [InlineData("{\"quantity\":\"abc\"}"), InlineData("{\"quantity\":\"\"}"), InlineData("{\"quantity\":null}")]
    public async Task InvalidQuantityJson_IsRejectedByApiBinding(string body)
    {
        await using var api = await Api.Create(102, "RetailCustomer");
        api.Data.AddCartLine();
        await Error(await api.Client.PutAsync("api/cart/items/701/quantity", new StringContent(body, Encoding.UTF8, "application/json")),
            HttpStatusCode.BadRequest);
        Assert.Equal(0, api.Data.Work.Commits); Assert.Equal(3, api.Data.Cart.CartItems.Single().Quantity);
    }
    [Theory, Trait("UseCase", "UC-RC-01")]
    [InlineData(0), InlineData(-1), InlineData(1.5)]
    public async Task InvalidQuantity_IsRejectedByServiceThroughApi(double quantity)
    {
        await using var api = await Api.Create(102, "RetailCustomer"); api.Data.AddCartLine();
        await Error(await api.Client.PutAsJsonAsync("api/cart/items/701/quantity", new QuantityRequest((decimal)quantity)),
            HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Equal(3, api.Data.Cart.CartItems.Single().Quantity); Assert.Equal(0, api.Data.Work.Commits);
    }
    [Fact, Trait("UseCase", "UC-RC-01")]
    public async Task CartQuantityUpdate_ReturnsWholesalePriceAndVietnameseNotification()
    {
        await using var api = await Api.Create(102, "RetailCustomer"); api.Data.AddCartLine(quantity: 9);
        var result = await Read<CartResponse>(await api.Client.PutAsJsonAsync("api/cart/items/701/quantity", new QuantityRequest(10)));
        Assert.Equal(25000, Assert.Single(result.Items).UnitPrice); Assert.Equal(250000, result.SelectedSubtotal);
        Assert.Contains(result.Messages, message => message.Contains("s\u1ec9")); Assert.All(result.Messages, m => Assert.DoesNotContain("(MSG", m));
    }
    [Fact, Trait("UseCase", "UC-RC-01")]
    public async Task CartPreparation_PriceChangeRequiresSecondReview()
    {
        await using var api = await Api.Create(102, "RetailCustomer"); api.Data.AddCartLine(quantity: 2);
        var initial = (await api.Client.GetFromJsonAsync<CartResponse>("api/cart"))!;
        api.Data.Product.BaseRetailPrice = 32000;
        var changed = await Read<CartResponse>(await api.Client.PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(initial.ReviewToken)));
        Assert.False(changed.CanProceedToCheckout); Assert.Equal(64000, changed.SelectedSubtotal);
        var reviewed = await Read<CartResponse>(await api.Client.PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(changed.ReviewToken)));
        Assert.True(reviewed.CanProceedToCheckout); Assert.Equal(5, api.Data.Batch.ReservedQuantity);
    }
    [Fact, Trait("UseCase", "UC-RC-01")]
    public async Task CartRemove_RemovesOnlyRequestedOwnedLine()
    {
        await using var api = await Api.Create(102, "RetailCustomer");
        api.Data.AddCartLine(701); api.Data.AddCartLine(702, selected: false);
        var cart = await Read<CartResponse>(await api.Client.PostAsJsonAsync("api/cart/remove-items", new RemoveCartItemsRequest([701])));
        Assert.Equal(702, Assert.Single(cart.Items).CartItemId); Assert.Equal(1, api.Data.Carts.Removals);
    }
    [Fact, Trait("UseCase", "UC-RC-01")]
    public async Task CartForeignItem_Returns404WithoutWrite()
    {
        await using var api = await Api.Create(103, "RetailCustomer"); api.Data.AddCartLine();
        await Error(await api.Client.PutAsJsonAsync("api/cart/items/701/quantity", new QuantityRequest(10)), HttpStatusCode.NotFound, "NOT_FOUND");
        Assert.Equal(0, api.Data.Work.Commits);
    }
    [Fact, Trait("UseCase", "UC-FAR-06")]
    public async Task OrderWorkflow_RoutesToActualTransitionsAndCreatesSingleDispatch()
    {
        await using var api = await Api.Create();
        foreach (var operation in new[] { "confirm", "ready", "dispatch" })
            await Read<SellerOrderResponse>(await api.Client.PostAsJsonAsync("api/farmer/orders/501/" + operation, api.Data.Action()));
        Assert.Equal("Shipping", api.Data.Seller.Status); Assert.Single(api.Data.Orders.Movements);
        Assert.Equal("InTransit", Assert.Single(api.Data.Seller.Shipments).ShipmentStatus);
        Assert.Equal(0, api.Data.Batch.ReservedQuantity); Assert.Equal(20, api.Data.Batch.AvailableQuantity);
    }
    [Fact, Trait("UseCase", "UC-FAR-06")]
    public async Task DispatchDuplicate_Returns409WithoutSecondShipment()
    {
        await using var api = await Api.Create(); api.Data.Seller.Status = "ReadyToShip";
        var action = api.Data.Action();
        await Read<SellerOrderResponse>(await api.Client.PostAsJsonAsync("api/farmer/orders/501/dispatch", action));
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/dispatch", action), HttpStatusCode.Conflict, "CONFLICT");
        Assert.Single(api.Data.Seller.Shipments); Assert.Single(api.Data.Orders.Movements);
    }
    [Theory, Trait("UseCase", "UC-FAR-06")]
    [InlineData(0), InlineData(14)]
    public async Task RejectShortExplanation_Returns400WithoutAudit(int length)
    {
        await using var api = await Api.Create();
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/reject",
            api.Data.Action("Shortfall", new string('a', length))), HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Empty(api.Data.Orders.Audits);
    }
    [Fact, Trait("UseCase", "UC-FAR-06")]
    public async Task FarmerCannotSetBuyerReceipt_UnsupportedActionReturns400()
    {
        await using var api = await Api.Create(); api.Data.Seller.Status = "Shipping";
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/receive", api.Data.Action()), HttpStatusCode.BadRequest, "VALIDATION");
        Assert.Null(api.Data.Seller.ReceivedAt); Assert.Empty(api.Data.Orders.Audits);
    }
    [Fact, Trait("UseCase", "UC-FAR-06")]
    public async Task OrderCommitFailure_Returns500WithNoRawMessageId()
    {
        await using var api = await Api.Create(); api.Data.Work.FailCommit = true;
        await Error(await api.Client.PostAsJsonAsync("api/farmer/orders/501/confirm", api.Data.Action()), HttpStatusCode.InternalServerError, "SERVER_ERROR");
        Assert.Equal(0, api.Data.Work.Commits);
        // In-memory ports cannot prove rollback; CommerceSqlTests verifies it against SQL.
    }
}
