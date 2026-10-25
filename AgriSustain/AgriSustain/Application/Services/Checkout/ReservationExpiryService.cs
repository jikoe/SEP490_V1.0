using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Checkout;

public sealed class ReservationExpiryService(ICheckoutRepository repository,
    IAdministrationRepository audit, IUseCaseUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<int> ExpireAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var orders = await repository.ExpiredOrdersAsync(clock.GetUtcNow(), 25, cancellationToken);
        foreach (var order in orders)
        {
            foreach (var seller in order.SellerOrders)
            {
                foreach (var item in seller.OrderItems)
                {
                    foreach (var allocation in item.OrderItemBatchAllocations
                        .Where(x => x.AllocationStatus == "Reserved"))
                    {
                        allocation.HarvestBatch.ReleaseReservation(allocation.AllocatedQuantity);
                        allocation.AllocationStatus = "Released";
                        allocation.ReleasedAt = now;
                        repository.AddMovement(new InventoryMovement
                        {
                            HarvestBatchId = allocation.HarvestBatchId,
                            OrderItemBatchAllocationId = allocation.OrderItemBatchAllocationId,
                            MovementType = "Release", QuantityChange = allocation.AllocatedQuantity,
                            Reason = "Payment reservation expired", OccurredAt = now,
                            RequestKey = $"expiry:{allocation.OrderItemBatchAllocationId}"
                        });
                    }
                    item.ItemStatus = "Cancelled";
                }
                seller.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OldStatus = seller.Status, NewStatus = "Cancelled", OccurredAt = now,
                    Reason = "Payment reservation expired"
                });
                seller.Status = "Cancelled";
            }
            foreach (var redemption in order.VoucherRedemptions.Where(x => x.Status == "Reserved"))
            {
                redemption.Status = "Released";
                redemption.ReleasedAt = now;
            }
            var heldPoints = order.RewardPointEntries.Sum(x => x.EntryType == "Hold" ? x.Points
                : x.EntryType is "Release" or "Consume" ? -x.Points : 0);
            if (heldPoints > 0)
            {
                repository.AddRewardEntry(new RewardPointEntry
                {
                    UserId = order.BuyerUserId, OrderId = order.OrderId, EntryType = "Release",
                    Points = heldPoints, RequestKey = $"expiry:{order.OrderId}", OccurredAt = now
                });
            }
            order.OrderStatus = "Cancelled";
            audit.AddAudit(new AuditLog
            {
                EntityType = "Order", EntityId = order.OrderId, Action = "ReservationExpired",
                OldValue = "PendingPayment", NewValue = "Cancelled", OccurredAt = now,
                Reason = "Payment reservation expired"
            });
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return orders.Count;
    }
}
