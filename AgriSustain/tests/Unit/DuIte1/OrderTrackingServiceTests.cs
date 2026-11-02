using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Validators.Orders;
using Xunit;

namespace AgriSustain.UnitTests.DuIte1;

/// <summary>UC-RC-04 Track Retail Order. Report 5.1 – Module 3.</summary>
public sealed class OrderTrackingServiceTests
{
    private const long Buyer = 10, OtherBuyer = 99;
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeClock _clock = new(Now);

    private OrderTrackingService Service(long? userId = Buyer) =>
        new(_uow, new FakeCurrentUser(userId), new OrderQueryValidator(), _clock);

    private static OrderItemDetailDto Item(long id, bool reviewed = false) =>
        new(id, 1, "Gạo ST25", 2, "kg", 50000, 100000, null, reviewed,
            [new OrderItemBatchDto("BN-20260920-01", 2, null, "Vụ Hè", null)]);

    private static OrderDetailDto Detail(long id, string status, string payment = "Unpaid",
        DateTimeOffset? deliveredAt = null, DateTimeOffset? reservationExpiresAt = null, bool reviewed = false) =>
        new(id, $"ORD-20260927-{id:000}", Now.AddDays(-1), status, "PayOS", "Standard",
            100000, 20000, 0, 120000, "Mai Tran", "0912345678", "123 Cầu Giấy, Hà Nội", null,
            payment, null,
            [new SellerOrderGroupDto(1, 1, "Nong Trai Xanh", "Đà Lạt", "SO-1", status, 120000, [Item(id, reviewed)])],
            [], deliveredAt, reservationExpiresAt);

    private static OrderSummaryDto Summary(long id, DateTimeOffset date, string status = "Completed") =>
        new(id, $"ORD-{id}", date, status, "PayOS", "Standard", 100000, 20000, 0, 120000,
            "Mai Tran", "0912345678", "Hà Nội", null, []);

    private void Add(OrderDetailDto order, long owner = Buyer)
    {
        _uow.OrderRepo.Details[order.OrderId] = order;
        _uow.OrderRepo.Owners[order.OrderId] = owner;
    }

    // ---------- GetOrderHistoryAsync ----------

    [Fact(DisplayName = "UTC022 History returns the buyer's orders, newest first")]
    public async Task UTC022_History_SortedNewestFirst()
    {
        _uow.OrderRepo.Summaries.AddRange([Summary(3, Now.AddDays(-4)), Summary(1, Now.AddDays(-1)), Summary(2, Now.AddDays(-2))]);

        var result = await Service().GetOrderHistoryAsync(new OrderQuery("All", null), default);

        Assert.Equal(3, result.Value!.Total);
        Assert.Equal(new long[] { 1, 2, 3 }, result.Value.Items.Select(x => x.OrderId));
    }

    [Fact(DisplayName = "UTC023 Status tab and keyword are passed to the query")]
    public async Task UTC023_FilterPassedThrough()
    {
        await Service().GetOrderHistoryAsync(new OrderQuery("Completed", "#ORD-20260925-002"), default);

        Assert.Equal("Completed", _uow.OrderRepo.LastQuery!.Status);
        Assert.Equal("#ORD-20260925-002", _uow.OrderRepo.LastQuery.Keyword);
    }

    [Fact(DisplayName = "UTC024 New customer gets an empty page (MSG23 shown by UI)")]
    public async Task UTC024_NoOrders_EmptyPage()
    {
        var result = await Service(15).GetOrderHistoryAsync(new OrderQuery("All", null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Total);
        Assert.Empty(result.Value.Items);
    }

    [Theory(DisplayName = "UTC-RC04-B1 Paging boundaries")]
    [InlineData(0, 10, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 100, true)]
    [InlineData(1, 101, false)]
    public async Task UTC_B1_PagingBoundaries(int page, int size, bool ok)
    {
        var result = await Service().GetOrderHistoryAsync(new OrderQuery(null, null, page, size), default);

        Assert.Equal(ok, result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-RC04-B2 Anonymous caller gets 401")]
    public async Task UTC_B2_Anonymous()
    {
        var result = await Service(null).GetOrderHistoryAsync(new OrderQuery(null, null), default);

        Assert.Equal(401, result.StatusCode);
    }

    // ---------- GetOrderDetailAsync ----------

    [Fact(DisplayName = "UTC025 Delivered order (12h ago) offers Review + Complaint and marks notifications read")]
    public async Task UTC025_DeliveredWithinWindow()
    {
        Add(Detail(101, "Delivered", "Paid", deliveredAt: Now.AddHours(-12)));

        var result = await Service().GetOrderDetailAsync(101, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("BN-20260920-01", result.Value!.SellerOrders[0].Items[0].Batches[0].BatchCode);
        Assert.Equal(new[] { OrderActions.Review, OrderActions.SubmitComplaint }, result.Value.AllowedActions);
        Assert.Contains((Buyer, 101L), _uow.OrderRepo.MarkedRead);
    }

    [Fact(DisplayName = "UTC026 PendingPayment order inside the window offers PayNow")]
    public async Task UTC026_PendingPayment_PayNow()
    {
        Add(Detail(102, "PendingPayment", reservationExpiresAt: Now.AddMinutes(10)));

        var result = await Service().GetOrderDetailAsync(102, default);

        Assert.Equal(new[] { OrderActions.PayNow }, result.Value!.AllowedActions);
    }

    [Fact(DisplayName = "UTC027 Another buyer's order is reported as not found and not marked read")]
    public async Task UTC027_OtherBuyer_NotFound()
    {
        Add(Detail(101, "Delivered"), owner: Buyer);

        var result = await Service(OtherBuyer).GetOrderDetailAsync(101, default);

        Assert.Equal(404, result.StatusCode);
        Assert.Empty(_uow.OrderRepo.MarkedRead);
    }

    [Fact(DisplayName = "UTC028 Non-existent order returns 404")]
    public async Task UTC028_Missing_NotFound()
    {
        var result = await Service().GetOrderDetailAsync(9999, default);

        Assert.Equal("ORDER_NOT_FOUND", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-RC04-B3 Lookup by code accepts a leading #")]
    public async Task UTC_B3_ByCode_WithHash()
    {
        Add(Detail(5, "Completed"));

        var result = await Service().GetOrderDetailByCodeAsync("#ORD-20260927-005", default);

        Assert.True(result.IsSuccess);
    }

    // ---------- ResolveActions (business rules) ----------

    [Theory(DisplayName = "UTC-RC04-B4 Complaint window is 48 hours after delivery (GBR-24)")]
    [InlineData(47, true)]
    [InlineData(48, true)]
    [InlineData(49, false)]
    public void UTC_B4_ComplaintWindow(int hoursAgo, bool allowed)
    {
        var order = Detail(1, "Delivered", "Paid", deliveredAt: Now.AddHours(-hoursAgo));

        var actions = OrderTrackingService.ResolveActions(order, Now);

        Assert.Equal(allowed, actions.Contains(OrderActions.SubmitComplaint));
    }

    [Fact(DisplayName = "UTC-RC04-B5 Expired payment window hides PayNow")]
    public void UTC_B5_ExpiredReservation_NoPayNow()
    {
        var order = Detail(1, "PendingPayment", reservationExpiresAt: Now.AddSeconds(-1));

        Assert.Empty(OrderTrackingService.ResolveActions(order, Now));
    }

    [Fact(DisplayName = "UTC-RC04-B6 Fully reviewed order hides Review")]
    public void UTC_B6_AllReviewed_NoReview()
    {
        var order = Detail(1, "Completed", "Paid", deliveredAt: Now.AddDays(-5), reviewed: true);

        Assert.Empty(OrderTrackingService.ResolveActions(order, Now));
    }

    [Theory(DisplayName = "UTC-RC04-B8 Non-completed orders never offer a complaint (MSG181)")]
    [InlineData("Shipping")]
    [InlineData("PendingPayment")]
    [InlineData("Processing")]
    public void UTC_B8_NotCompleted_NoComplaint(string status)
    {
        // Even with a recent delivery timestamp, only Delivered/Completed orders are eligible.
        var order = Detail(1, status, deliveredAt: Now.AddHours(-1), reservationExpiresAt: Now.AddMinutes(10));

        Assert.DoesNotContain(OrderActions.SubmitComplaint, OrderTrackingService.ResolveActions(order, Now));
    }

    [Fact(DisplayName = "UTC-RC04-B7 Cancelled order has no actions")]
    public void UTC_B7_Cancelled_NoActions()
    {
        Assert.Empty(OrderTrackingService.ResolveActions(Detail(1, "Cancelled"), Now));
    }
}
