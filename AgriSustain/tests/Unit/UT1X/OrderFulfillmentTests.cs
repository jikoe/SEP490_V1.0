using AgriSustain.Domain.Entities;
using Xunit;
namespace AgriSustain.UnitTests.UT1X;
[Trait("UseCase", "UC-FAR-06")]
public sealed class OrderFulfillmentTests
{
    [Fact, Trait("CaseId", "UT-FAR-06-R2-001")]

    public async Task Confirm_ReusesReservation()
    {
        var f = new CommerceFixture();
        Assert.True((await f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default)).IsSuccess);
        Assert.Equal("Confirmed", f.Seller.Status); Assert.Equal(5, f.Batch.ReservedQuantity); Assert.Equal(20, f.Batch.AvailableQuantity);
        Assert.Empty(f.Orders.Movements); Assert.Single(f.Seller.OrderStatusHistories); Assert.Equal(1, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-002")]
    public async Task Confirm_UnpaidRetail_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Seller.Order.PaymentTransactions.Clear(); await Denied(f, "confirm", "Pending", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-003")]
    public async Task Confirm_VerifiedPreorderDeposit_IsAccepted()
    {
        var f = new CommerceFixture();
        f.Seller.Order.IsPreOrder = true; f.Seller.Order.OrderType = "PreOrder";
        f.Line.PreOrderReservation = new PreOrderReservation { DepositPercentageSnapshot = 25, Quantity = 5 };
        f.Seller.Order.PaymentTransactions.Single().Amount = 37500;
        Assert.True((await f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default)).IsSuccess); Assert.Equal(5, f.Batch.ReservedQuantity);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-004")]
    public async Task Confirm_PreorderWithoutAllocation_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Seller.Order.IsPreOrder = true; f.Line.OrderItemBatchAllocations.Clear(); await Denied(f, "confirm", "Pending", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-005")]
    public async Task Confirm_Expired_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Batch.Status = "Expired";
        var result = await f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default);
        Assert.False(result.IsSuccess); Assert.Contains(f.Batch.BatchCode, result.Error!.Message);
        Assert.Equal("Pending", f.Seller.Status); Assert.Equal(0, f.Work.Commits); Assert.Empty(f.Orders.Movements);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-006")]
    public async Task Confirm_Recalled_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Batch.Status = "Recalled";
        var result = await f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default);
        Assert.False(result.IsSuccess); Assert.Contains(f.Batch.BatchCode, result.Error!.Message);
        Assert.Equal("Pending", f.Seller.Status); Assert.Equal(0, f.Work.Commits); Assert.Empty(f.Orders.Movements);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-007")]
    public async Task Confirm_Blocked_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Batch.Status = "Blocked";
        var result = await f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default);
        Assert.False(result.IsSuccess); Assert.Contains(f.Batch.BatchCode, result.Error!.Message);
        Assert.Equal("Pending", f.Seller.Status); Assert.Equal(0, f.Work.Commits); Assert.Empty(f.Orders.Movements);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-008")]
    public async Task Confirm_RepeatedRequest_HasNoDuplicateEffects()
    {
        var f = new CommerceFixture();
        var request = f.Action(); Assert.True((await f.OrderService.ActAsync(101, 501, "confirm", request, default)).IsSuccess);
        Assert.Equal("CONFLICT", (await f.OrderService.ActAsync(101, 501, "confirm", request, default)).Error!.Code);
        Assert.Equal(1, f.Work.Commits); Assert.Single(f.Seller.OrderStatusHistories); Assert.Single(f.Orders.Notifications);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-009")]
    public async Task Confirm_ForeignOrder_IsDenied()
    {
        var f = new CommerceFixture();
        await Denied(f, "confirm", "Pending", "NOT_FOUND", 999);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-010")]
    public async Task Confirm_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.Work.FailCommit = true; await Assert.ThrowsAsync<IOException>(() => f.OrderService.ActAsync(101, 501, "confirm", f.Action(), default)); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-011")]
    public async Task Ready_DoesNotConsumeStock()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; Assert.True((await f.OrderService.ActAsync(101, 501, "ready", f.Action(), default)).IsSuccess);
        Assert.Equal("ReadyToShip", f.Seller.Status); Assert.Equal(5, f.Batch.ReservedQuantity); Assert.Empty(f.Orders.Movements); Assert.Empty(f.Seller.Shipments); Assert.Single(f.Seller.OrderStatusHistories);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-012")]
    public async Task Ready_FromPending_IsRejected()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Pending"; await Denied(f, "ready", "Pending", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-013")]
    public async Task Ready_FromCancelled_IsRejected()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Cancelled"; await Denied(f, "ready", "Cancelled", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-014")]
    public async Task Ready_FromShipping_IsRejected()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Shipping"; await Denied(f, "ready", "Shipping", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-015")]
    public async Task Ready_OtherFarm_IsDenied()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; await Denied(f, "ready", "Confirmed", "NOT_FOUND", 999);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-016")]
    public async Task Ready_RepeatedRequest_HasNoDuplicateEvent()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; var request = f.Action(); Assert.True((await f.OrderService.ActAsync(101, 501, "ready", request, default)).IsSuccess);
        Assert.Equal("CONFLICT", (await f.OrderService.ActAsync(101, 501, "ready", request, default)).Error!.Code); Assert.Single(f.Seller.OrderStatusHistories); Assert.Equal(1, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-017")]
    public async Task Ready_UnknownOrder_IsDenied()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; await Denied(f, "ready", "Confirmed", "NOT_FOUND", 998);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-018")]
    public async Task Ready_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; f.Work.FailCommit = true; await Assert.ThrowsAsync<IOException>(() => f.OrderService.ActAsync(101, 501, "ready", f.Action(), default)); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-019")]
    public async Task Dispatch_ConsumesOnlyReservedStockOnce()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; Assert.True((await f.OrderService.ActAsync(101, 501, "dispatch", f.Action(), default)).IsSuccess);
        Assert.Equal("Shipping", f.Seller.Status); Assert.Equal("InTransit", Assert.Single(f.Seller.Shipments).ShipmentStatus);
        Assert.Equal(0, f.Batch.ReservedQuantity); Assert.Equal(20, f.Batch.AvailableQuantity); Assert.Equal(-5, Assert.Single(f.Orders.Movements).QuantityChange);
        Assert.Equal("Consumed", f.Allocation.AllocationStatus); Assert.Single(f.Orders.Notifications); Assert.Equal("Shipping", f.Seller.Order.OrderStatus);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-020")]
    public async Task Dispatch_MissingAllocation_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; f.Line.OrderItemBatchAllocations.Clear(); await Denied(f, "dispatch", "ReadyToShip", "CONFLICT"); Assert.Empty(f.Seller.Shipments);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-021")]
    public async Task Dispatch_RecalledBatch_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; f.Batch.Status = "Recalled"; await Denied(f, "dispatch", "ReadyToShip", "CONFLICT"); Assert.Equal(5, f.Batch.ReservedQuantity);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-022")]
    public async Task Dispatch_BeforeReadiness_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "Confirmed"; await Denied(f, "dispatch", "Confirmed", "CONFLICT"); Assert.Empty(f.Seller.Shipments);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-023")]
    public async Task Dispatch_RepeatedRequest_DoesNotConsumeTwice()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; var request = f.Action(); Assert.True((await f.OrderService.ActAsync(101, 501, "dispatch", request, default)).IsSuccess);
        Assert.Equal("CONFLICT", (await f.OrderService.ActAsync(101, 501, "dispatch", request, default)).Error!.Code);
        Assert.Single(f.Seller.Shipments); Assert.Single(f.Orders.Movements); Assert.Single(f.Orders.Notifications); Assert.Equal(0, f.Batch.ReservedQuantity); Assert.Equal(1, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-024")]
    public async Task Dispatch_CommitFailure_NeverReturnsSuccess()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; f.Work.FailCommit = true; await Assert.ThrowsAsync<IOException>(() => f.OrderService.ActAsync(101, 501, "dispatch", f.Action(), default));
        Assert.Equal(0, f.Work.Commits); Assert.Equal(1, f.Work.Attempts); // SQL tests verify rollback of staged mutations.
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-025")]
    public async Task Dispatch_ForeignOrder_IsDenied()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; await Denied(f, "dispatch", "ReadyToShip", "NOT_FOUND", 999);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R2-026")]
    public async Task Dispatch_PersistenceException_Propagates()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip"; f.Work.FailCommit = true; await Assert.ThrowsAsync<IOException>(() => f.OrderService.ActAsync(101, 501, "dispatch", f.Action(), default)); Assert.Equal(0, f.Work.Commits); Assert.Equal(1, f.Work.Attempts);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-001")]
    public async Task Reject_PaidOrder_CreatesPendingRefundAndNotifiesFinanceAndCustomer()
    {
        var f = new CommerceFixture();
        var result = await f.OrderService.ActAsync(101, 501, "reject", f.Action(explanation: "Hết hàng tươi trong ngày"), default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Cancelled", f.Seller.Status);
        Assert.Equal(0, f.Batch.ReservedQuantity);
        var refund = Assert.Single(f.Orders.SavedRefunds);
        Assert.Equal(RefundTransaction.StatusPending, refund.RefundStatus);
        Assert.Equal(150000, refund.Amount);
        Assert.Equal(2, f.Orders.SavedNotifications.Count);
        Assert.Single(f.Orders.SavedNotifications, n => n.DeliveryChannel == "InApp" && n.DeliveryStatus == "Sent");
        Assert.Single(f.Orders.SavedNotifications, n => n.DeliveryChannel == "Email" && n.DeliveryStatus == "Pending");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-002")]
    public async Task Reject_ReasonLengthBoundary_500Accepted501Rejected()
    {
        var f1 = new CommerceFixture();
        var ok = await f1.OrderService.ActAsync(101, 501, "reject", f1.Action(explanation: new string('a', 500)), default);
        Assert.True(ok.IsSuccess, ok.Error?.Message);

        var f2 = new CommerceFixture();
        var bad = await f2.OrderService.ActAsync(101, 501, "reject", f2.Action(explanation: new string('a', 501)), default);
        Assert.False(bad.IsSuccess);
        Assert.Equal("VALIDATION", bad.Error!.Code);
        Assert.Equal("Pending", f2.Seller.Status);
        Assert.Equal(0, f2.Work.Commits);
    }

    [Theory, Trait("CaseId", "UT-FAR-06-R3-003")]
    [InlineData("Shipping")]
    [InlineData("Delivered")]
    [InlineData("Cancelled")]
    public async Task Reject_FromInvalidStatus_IsBlocked(string status)
    {
        var f = new CommerceFixture();
        f.Seller.Status = status;
        var res = await f.OrderService.ActAsync(101, 501, "reject", f.Action(explanation: "Cannot fulfill"), default);
        Assert.False(res.IsSuccess);
        Assert.Equal("CONFLICT", res.Error!.Code);
        Assert.Equal(status, f.Seller.Status);
        Assert.Empty(f.Orders.SavedRefunds);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-004")]
    public async Task Action_UnapprovedFarmerOrContractOrder_IsBlocked()
    {
        var f1 = new CommerceFixture();
        f1.Orders.Approved = false;
        await Denied(f1, "confirm", "Pending", "FORBIDDEN");

        var f2 = new CommerceFixture();
        f2.Seller.B2bcontractId = 99;
        await Denied(f2, "confirm", "Pending", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-005")]
    public async Task Action_InvalidRequestKeyVersionOrUnsupportedAction_ReturnsValidation()
    {
        var f = new CommerceFixture();
        var badKey = await f.OrderService.ActAsync(101, 501, "confirm", f.Action() with { RequestKey = "invalid" }, default);
        Assert.Equal("VALIDATION", badKey.Error!.Code);

        var badVer = await f.OrderService.ActAsync(101, 501, "confirm", f.Action() with { Version = "short" }, default);
        Assert.Equal("VALIDATION", badVer.Error!.Code);

        var badAct = await f.OrderService.ActAsync(101, 501, "deliver", f.Action(), default);
        Assert.Equal("VALIDATION", badAct.Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-006")]
    public async Task Confirm_ZeroQuantityOrExpiredBatchBoundary_IsBlocked()
    {
        var f1 = new CommerceFixture();
        f1.Line.Quantity = 0;
        await Denied(f1, "confirm", "Pending", "VALIDATION");

        var f2 = new CommerceFixture();
        f2.Batch.ExpirationDate = DateOnly.FromDateTime(f2.Clock.Now.AddHours(7).DateTime);
        await Denied(f2, "confirm", "Pending", "CONFLICT");
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-007")]
    public async Task StartShippingAlias_TransitionsReadyOrderToShipping()
    {
        var f = new CommerceFixture();
        f.Seller.Status = "ReadyToShip";
        var res = await f.OrderService.ActAsync(101, 501, "start-shipping", f.Action(), default);
        Assert.True(res.IsSuccess);
        Assert.Equal("Shipping", f.Seller.Status);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-008")]
    public async Task ListOrders_InvalidDateRangeAndFilters_ReturnsValidation()
    {
        var f = new CommerceFixture();
        var ok = await f.OrderService.GetListAsync(101, new Application.DTOs.Orders.SellerOrderQuery { Page = 1, PageSize = 10 }, default);
        Assert.True(ok.IsSuccess);

        var badDates = await f.OrderService.GetListAsync(101, new Application.DTOs.Orders.SellerOrderQuery
        {
            From = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            To = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero)
        }, default);
        Assert.False(badDates.IsSuccess);
        Assert.True(badDates.Error!.Fields!.ContainsKey("to"));

        var badStatus = await f.OrderService.GetListAsync(101, new Application.DTOs.Orders.SellerOrderQuery { Status = "Unknown" }, default);
        Assert.Equal("VALIDATION", badStatus.Error!.Code);
    }

    [Fact, Trait("CaseId", "UT-FAR-06-R3-009")]
    public async Task PackingSlip_OwnedReturnsSnapshotAndForeignDenied()
    {
        var f = new CommerceFixture();
        var ok = await f.FarmerOrderService.GetPackingSlipAsync(101, 501, default);
        Assert.True(ok.IsSuccess);
        Assert.Equal(150000, ok.Value!.FinalAmount);
        Assert.Equal("Pending", f.Seller.Status);

        var denied = await f.FarmerOrderService.GetPackingSlipAsync(101, 999, default);
        Assert.Equal("NOT_FOUND", denied.Error!.Code);
    }

    private static async Task Denied(CommerceFixture f, string action, string original, string code, long id = 501)
    {
        var result = await f.OrderService.ActAsync(101, id, action, f.Action(), default);
        Assert.False(result.IsSuccess); Assert.Equal(code, result.Error!.Code); Assert.Equal(original, f.Seller.Status);
        Assert.Equal(0, f.Work.Commits); Assert.Empty(f.Orders.Movements); Assert.Empty(f.Orders.Notifications); Assert.DoesNotContain("(MSG", result.Error.Message);
    }
}

