using AgriSustain.Domain.Entities;
using Xunit;
namespace AgriSustain.UnitTests.Commerce;
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

    private static async Task Denied(CommerceFixture f, string action, string original, string code, long id = 501)
    {
        var result = await f.OrderService.ActAsync(101, id, action, f.Action(), default);
        Assert.False(result.IsSuccess); Assert.Equal(code, result.Error!.Code); Assert.Equal(original, f.Seller.Status);
        Assert.Equal(0, f.Work.Commits); Assert.Empty(f.Orders.Movements); Assert.Empty(f.Orders.Notifications); Assert.DoesNotContain("(MSG", result.Error.Message);
    }
}

