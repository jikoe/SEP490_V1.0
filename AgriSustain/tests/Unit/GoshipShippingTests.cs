using System.Net;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Infrastructure.Shipping;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class GoshipShippingTests
{
    private const string Rates = """
        {"code":200,"status":"success","data":[
          {"id":"a","carrier_short_name":"best","service":"Nhanh","total_fee":30000},
          {"id":"b","carrier_short_name":"ghtk","service":"Chậm","total_fee":19540},
          {"id":"c","carrier_short_name":"ahamove","service":"Hỏa tốc","total_fee":25000}]}
        """;

    [Theory]
    [InlineData("Standard", 19540)]
    [InlineData("FreshExpress", 25000)]
    public async Task GetRate_SelectsCheapestRateForDeliveryMethod(string method, decimal fee)
    {
        var handler = new StubHandler(HttpStatusCode.OK, Rates);
        var result = await Provider(handler).GetRateAsync(Request(method), default);

        Assert.Equal(new ShippingRateResult(true, fee), result);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://sandbox.goship.io/api/v2/rates", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        using var body = JsonDocument.Parse(handler.Body!);
        var shipment = body.RootElement.GetProperty("shipment");
        Assert.Equal("700000", shipment.GetProperty("address_from").GetProperty("city").GetString());
        Assert.Equal("700100", shipment.GetProperty("address_from").GetProperty("district").GetString());
        Assert.Equal("100000", shipment.GetProperty("address_to").GetProperty("city").GetString());
        Assert.Equal("100300", shipment.GetProperty("address_to").GetProperty("district").GetString());
        var parcel = shipment.GetProperty("parcel");
        Assert.Equal(1500, parcel.GetProperty("weight").GetInt32());
        Assert.Equal(120000, parcel.GetProperty("amount").GetInt64());
        Assert.Equal(0, parcel.GetProperty("cod").GetInt64());
    }

    [Fact]
    public async Task GetRate_NoFastService_IsNotSupported()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"code":200,"status":"success","data":[{"id":"b","service":"Chậm","total_fee":19540}]}
            """);
        Assert.Equal(new ShippingRateResult(false, null),
            await Provider(handler).GetRateAsync(Request("FreshExpress"), default));
    }

    [Fact]
    public async Task GetRate_ValidationError_IsUnavailable()
    {
        var handler = new StubHandler(HttpStatusCode.UnprocessableEntity,
            """{"code":422,"status":"error","data":{"errors":{}}}""");
        Assert.Null(await Provider(handler).GetRateAsync(Request("Standard"), default));
    }

    [Fact]
    public async Task GetRate_MissingPickupLocation_DoesNotCallGoship()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Rates);
        Assert.Null(await Provider(handler).GetRateAsync(
            Request("Standard") with { FarmerProfileId = 99 }, default));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Normalize_ChecksHierarchyAndUsesGoshipNames()
    {
        var handler = new RoutingHandler(new Dictionary<string, string>
        {
            ["/api/v2/cities"] = """{"code":200,"data":[{"id":"100000","name":"Hà Nội"}]}""",
            ["/api/v2/cities/100000/districts"] =
                """{"code":200,"data":[{"id":"100300","name":"Quận Hai Bà Trưng","city_id":"100000"}]}""",
            ["/api/v2/districts/100300/wards"] =
                """{"code":200,"data":[{"id":19,"name":"Phường  Quỳnh Lôi ","district_id":"100300"}]}"""
        });
        var catalog = new GoshipAddressCatalog(Client(handler),
            new MemoryCache(new MemoryCacheOptions()), Options.Create(new GoshipOptions()));

        var normalized = await catalog.NormalizeAsync(Address(), default);
        Assert.NotNull(normalized);
        Assert.Equal(("Hà Nội", "Quận Hai Bà Trưng", "Phường  Quỳnh Lôi", GoshipAddressCatalog.Version),
            (normalized.Province, normalized.District, normalized.Ward, normalized.CatalogVersion));
        Assert.Null(await catalog.NormalizeAsync(Address() with { WardCode = "20" }, default));
        Assert.Null(await catalog.NormalizeAsync(Address() with { DistrictCode = "" }, default));
    }

    private static GoshipShippingRateProvider Provider(HttpMessageHandler handler) =>
        new(Client(handler), Options.Create(new GoshipOptions
        {
            Enabled = true, AccessToken = "token",
            PickupLocations = { ["7"] = new GoshipPickupLocationOptions { City = "700000", District = "700100" } }
        }));

    private static GoshipClient Client(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.goship.io/api/v2/") };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");
        return new GoshipClient(http);
    }

    private static CheckoutAddress Address() => new("Recipient", "0912345678", "100000", "",
        "100300", "", "19", "", "Detail");

    private static ShippingRateRequest Request(string method) => new(7, Address(),
        [new ShippingPackageLine("kg", 1.2m), new ShippingPackageLine("g", 300)], 119999.5m, method);

    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RoutingHandler(IReadOnlyDictionary<string, string> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(
            routes.TryGetValue(request.RequestUri!.AbsolutePath, out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
