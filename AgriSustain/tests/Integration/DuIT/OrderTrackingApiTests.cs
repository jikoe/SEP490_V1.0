using System.Net;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-RC-04 Track Retail Order: order history, order details and "Đã nhận được hàng" against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-RC-04")]
public sealed class OrderTrackingApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #01] Order history lists only the customer's own orders, newest first")]
    public async Task History_OwnOrders_NewestFirst()
    {
        var json = await ApiAssert.Status(await _api.Customer().GetAsync("api/orders?status=All&page=1&pageSize=10"),
            HttpStatusCode.OK);

        Assert.Equal(4, json.GetProperty("total").GetInt32());
        var codes = json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("orderCode").GetString()).ToList();
        Assert.Equal(new[] { "ORD-IT-004", "ORD-IT-001", "ORD-IT-003", "ORD-IT-002" }, codes);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #02] The status tab and the keyword filter the SQL query")]
    public async Task History_StatusAndKeyword_Filtered()
    {
        var byStatus = await ApiAssert.Status(await _api.Customer().GetAsync("api/orders?status=Completed"), HttpStatusCode.OK);
        var byKeyword = await ApiAssert.Status(await _api.Customer().GetAsync("api/orders?keyword=%23ORD-IT-003"), HttpStatusCode.OK);

        Assert.Equal("ORD-IT-002", Assert.Single(byStatus.GetProperty("items").EnumerateArray()).GetProperty("orderCode").GetString());
        Assert.Equal("ORD-IT-003", Assert.Single(byKeyword.GetProperty("items").EnumerateArray()).GetProperty("orderCode").GetString());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #03] A page size above 100 is refused")]
    public async Task History_PageSizeTooLarge_Rejected()
    {
        var response = await _api.Customer().GetAsync("api/orders?page=1&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #04] Order details of a delivered order offer Confirm Received")]
    public async Task Detail_Delivered_OffersConfirmReceived()
    {
        var json = await ApiAssert.Status(await _api.Customer().GetAsync($"api/orders/{_api.DeliveredOrderId}"), HttpStatusCode.OK);

        Assert.Equal(("ORD-IT-001", "Delivered"), (json.GetProperty("orderCode").GetString(), json.GetProperty("orderStatus").GetString()));
        Assert.Contains(json.GetProperty("allowedActions").EnumerateArray(), a => a.GetString() == "ConfirmReceived");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #05] Order details are found by the displayed code with a leading #")]
    public async Task Detail_ByCodeWithHash_Found()
    {
        var json = await ApiAssert.Status(await _api.Customer().GetAsync("api/orders/by-code/%23ORD-IT-002"), HttpStatusCode.OK);

        Assert.Equal(_api.CompletedOrderId, json.GetProperty("orderId").GetInt64());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #06] Another customer's order is reported as not found")]
    public async Task Detail_OtherCustomer_NotFound()
    {
        var response = await _api.Customer().GetAsync($"api/orders/{_api.OtherOrderId}");

        await ApiAssert.Problem(response, HttpStatusCode.NotFound, "ORDER_NOT_FOUND");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #07] Confirm Received completes the order and its seller order in SQL")]
    public async Task ConfirmReceived_Delivered_Completed()
    {
        var response = await _api.Customer().PostAsync($"api/orders/{_api.DeliveredOrderId}/confirm-received", null);

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var order = await db.Orders.Include(o => o.SellerOrders).SingleAsync(o => o.OrderId == _api.DeliveredOrderId);
        Assert.Equal("Completed", order.OrderStatus);
        Assert.NotNull(order.CompletedAt);
        var seller = Assert.Single(order.SellerOrders);
        Assert.Equal("Completed", seller.Status);
        Assert.NotNull(seller.ReceivedAt);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #08] An order that is not delivered yet cannot be confirmed")]
    public async Task ConfirmReceived_Processing_Rejected()
    {
        var response = await _api.Customer().PostAsync($"api/orders/{_api.ProcessingOrderId}/confirm-received", null);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "ORDER_NOT_DELIVERED");
        await using var db = _api.Open();
        Assert.Equal("Processing", (await db.Orders.SingleAsync(o => o.OrderId == _api.ProcessingOrderId)).OrderStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #09] An order with an open complaint cannot be confirmed")]
    public async Task ConfirmReceived_OpenComplaint_Rejected()
    {
        var response = await _api.Customer().PostAsync($"api/orders/{_api.DisputedOrderId}/confirm-received", null);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "COMPLAINT_IN_PROGRESS");
        await using var db = _api.Open();
        Assert.Equal("Delivered", (await db.Orders.SingleAsync(o => o.OrderId == _api.DisputedOrderId)).OrderStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-RC-04 #10] Signed-out visitors cannot read order history")]
    public async Task History_Anonymous_Unauthorized()
    {
        var response = await _api.Anonymous().GetAsync("api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
