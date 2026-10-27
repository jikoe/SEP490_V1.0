using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class OrderRepository(AgriSustainContext context, FulfillmentRules rules,
    TimeProvider clock) : IOrderRepository
{
    private IQueryable<SellerOrder> Details() => context.SellerOrders
        .Include(x => x.FarmerProfile).ThenInclude(x => x.User)
        .Include(x => x.Order).ThenInclude(x => x.PaymentTransactions)
            .ThenInclude(x => x.RefundTransactions)
        .Include(x => x.Order).ThenInclude(x => x.SellerOrders)
            .ThenInclude(x => x.OrderItems).ThenInclude(x => x.PreOrderReservation)
        .Include(x => x.OrderItems).ThenInclude(x => x.Product).ThenInclude(x => x.ProductImages)
        .Include(x => x.OrderItems).ThenInclude(x => x.OrderItemBatchAllocations)
            .ThenInclude(x => x.HarvestBatch)
        .Include(x => x.Shipments).ThenInclude(x => x.ShipmentItems)
        .Include(x => x.OrderStatusHistories)
        .Include(x => x.DisputeTickets).ThenInclude(x => x.DisputeEvidenceRequests)
        .AsSplitQuery();

    public Task<bool> IsApprovedFarmerAsync(long userId, CancellationToken cancellationToken) =>
        context.FarmerProfiles.AnyAsync(x => x.UserId == userId
            && x.VerificationStatus == "Approved", cancellationToken);

    public Task<SellerOrder?> GetOwnedAsync(long userId, long id,
        CancellationToken cancellationToken) => Details().SingleOrDefaultAsync(x =>
            x.SellerOrderId == id && x.FarmerProfile.UserId == userId, cancellationToken);

    public async Task<PageResult<SellerOrderResponse>> SearchOwnedAsync(long userId,
        SellerOrderQuery query, CancellationToken cancellationToken)
    {
        var rows = Details().AsNoTrackingWithIdentityResolution().Where(x => x.FarmerProfile.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            rows = rows.Where(x => x.SellerOrderCode.Contains(query.Keyword)
                || x.Order.OrderCode.Contains(query.Keyword)
                || x.Order.RecipientNameSnapshot.Contains(query.Keyword)
                || x.OrderItems.Any(item => item.OrderItemBatchAllocations
                    .Any(allocation => allocation.HarvestBatch.BatchCode.Contains(query.Keyword))));
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            rows = rows.Where(x => x.Status == query.Status);
        }

        if (!string.IsNullOrEmpty(query.OrderType))
        {
            rows = rows.Where(x => x.Order.OrderType == query.OrderType);
        }

        if (!string.IsNullOrEmpty(query.PaymentStatus))
        {
            bool paid = query.PaymentStatus == "Paid";
            rows = rows.Where(x => x.Order.PaymentTransactions.Any(p =>
                p.PaymentStatus == "Success" || p.PaymentStatus == "Succeeded"
                || p.PaymentStatus == "Paid" || p.PaymentStatus == "Completed") == paid);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value.UtcDateTime;
            rows = rows.Where(x => x.Order.OrderDate >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value.UtcDateTime;
            rows = rows.Where(x => x.Order.OrderDate < to);
        }

        int total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(x => x.Order.OrderDate)
            .ThenByDescending(x => x.SellerOrderId).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).ToListAsync(cancellationToken);
        return new PageResult<SellerOrderResponse>(items.Select(Map).ToList(),
            total, query.Page, query.PageSize);
    }

    public Task<bool> HasRequestAsync(string requestKey, CancellationToken cancellationToken) =>
        context.AuditLogs.AnyAsync(x => x.EntityType == "SellerOrder"
            && x.TraceId == requestKey, cancellationToken);

    public async Task ReleasePurchaseHoldsAsync(SellerOrder order,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var reservations = await context.PreOrderReservations
            .Include(x => x.PreOrderCampaign).Where(x => x.OrderItem.SellerOrderId
                == order.SellerOrderId && (x.ReservationStatus == "Reserved"
                    || x.ReservationStatus == "Confirmed"))
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            if (reservation.ReservationStatus == "Confirmed")
            {
                if (reservation.PreOrderCampaign.CommittedYield < reservation.Quantity)
                    throw new InvalidOperationException("Inconsistent committed preorder quantity.");
                reservation.PreOrderCampaign.CommittedYield -= reservation.Quantity;
            }

            reservation.ReservationStatus = "Released";
        }

        bool allCancelled = order.Order.SellerOrders.All(x =>
            x.SellerOrderId == order.SellerOrderId || x.Status is "Cancelled" or "Rejected");
        var vouchers = await context.VoucherRedemptions.Where(x => x.OrderId == order.OrderId
            && x.Status == "Reserved" && (x.SellerOrderId == order.SellerOrderId
                || allCancelled)).ToListAsync(cancellationToken);
        foreach (var voucher in vouchers)
        {
            voucher.Status = "Released";
            voucher.ReleasedAt = now;
        }

        if (allCancelled)
        {
            var entries = await context.RewardPointEntries.Where(x => x.OrderId == order.OrderId)
                .ToListAsync(cancellationToken);
            long held = entries.Sum(x => x.HeldDelta ?? 0);
            string key = $"seller-cancel:{order.OrderId}:points";
            if (held > 0 && !entries.Any(x => x.RequestKey == key))
            {
                context.RewardPointEntries.Add(new RewardPointEntry
                {
                    UserId = order.Order.BuyerUserId, OrderId = order.OrderId,
                    EntryType = "Release", Points = checked((int)held), AvailableDelta = held,
                    HeldDelta = -held, RequestKey = key, OccurredAt = now
                });
            }
        }
    }

    public async Task QueueFinanceReviewAsync(SellerOrder order, long actorId,
        CancellationToken cancellationToken)
    {
        var recipients = await context.Users.Where(user => user.Status == "Active"
            && (user.Role == "FinanceStaff" || user.Role == "Finance Staff"))
            .Select(user => user.UserId).ToListAsync(cancellationToken);
        foreach (long recipient in recipients)
        {
            Notify(recipient, "Yêu cầu đối soát hoàn tiền",
                $"Đơn {order.SellerOrderCode} đã hủy sau khi thu tiền; cần duyệt hoàn tiền.",
                order.SellerOrderId);
        }

        AddAudit(new AuditLog
        {
            PerformedByUserId = actorId, EntityType = "SellerOrder",
            EntityId = order.SellerOrderId, Action = "RefundReviewRequested",
            NewValue = "PendingFinanceReview", OccurredAt = clock.GetUtcNow().UtcDateTime
        });
    }

    public async Task NotifySupportAsync(long sellerOrderId, string content,
        CancellationToken cancellationToken)
    {
        var recipients = await context.Users.Where(user => user.Status == "Active"
            && (user.Role == "CustomerSupport" || user.Role == "Customer Support"))
            .Select(user => user.UserId).ToListAsync(cancellationToken);
        foreach (long userId in recipients)
            Notify(userId, "Yêu cầu hỗ trợ đơn hàng", content, sellerOrderId);
    }

    public void AddMovement(InventoryMovement movement) => context.InventoryMovements.Add(movement);
    public void AddAudit(AuditLog audit) => context.AuditLogs.Add(audit);
    public void AddEvidence(DisputeEvidence evidence) => context.DisputeEvidences.Add(evidence);

    public void Notify(long userId, string title, string content, long sellerOrderId) =>
        context.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Content = content, NotificationType = "Order",
            DeliveryChannel = "InApp", DeliveryStatus = "Sent", RelatedRecordType = "SellerOrder",
            RelatedRecordId = sellerOrderId, SentAt = clock.GetUtcNow().UtcDateTime
        });

    public SellerOrderResponse Map(SellerOrder order)
    {
        bool paid = rules.PaymentSatisfied(order);
        bool allocated = rules.AllocationsValid(order);
        bool contract = order.B2bcontractId.HasValue;
        return new SellerOrderResponse(order.SellerOrderId, order.SellerOrderCode,
            order.OrderId, order.Order.OrderCode, order.Order.OrderType, order.Order.OrderDate,
            order.Status, order.Order.OrderStatus, order.Order.RecipientNameSnapshot,
            order.Order.RecipientPhoneSnapshot, order.Order.ShippingAddress,
            order.Order.OrderNotes, order.Subtotal, order.DiscountShare, order.DeliveryFee,
            order.FinalAmount, paid ? "Paid" : "AwaitingPayment", rules.VerifiedPayment(order),
            rules.RequiredPayment(order), order.DeliveredAt, order.DeliveredAt?.AddHours(48),
            order.ReceivedAt, FulfillmentRules.Version(order),
            !contract && order.Status == "Pending" && paid && allocated,
            !contract && FulfillmentRules.IsConfirmed(order.Status) && paid && allocated,
            !contract && FulfillmentRules.IsReady(order.Status) && paid && allocated,
            !contract && FulfillmentRules.IsBeforeShipping(order.Status),
            order.OrderItems.Select(item => new OrderLineResponse(item.OrderItemId,
                item.ProductNameSnapshot ?? item.Product.ProductName, item.UnitSnapshot,
                item.Quantity, item.UnitPriceSnapshot, item.SubTotal,
                item.Product.ProductImages.FirstOrDefault(x => x.IsPrimary)?.ImageUrl,
                item.OrderItemBatchAllocations.Select(a => new AllocationResponse(
                    a.HarvestBatch.BatchCode, a.HarvestBatch.ExpirationDate,
                    a.AllocatedQuantity, a.AllocationStatus, a.HarvestBatch.Status)).ToList())).ToList(),
            order.Shipments.Select(x => new ShipmentResponse(x.ShipmentId, x.ShipmentStatus,
                x.ShippedAt, x.DeliveredAt, x.ReceivedAt)).ToList(),
            order.OrderStatusHistories.OrderBy(x => x.OccurredAt)
                .Select(x => new TimelineResponse(x.OldStatus, x.NewStatus,
                    x.OccurredAt, x.Reason)).ToList(),
            order.DisputeTickets.Select(x => new ComplaintResponse(x.DisputeTicketId,
                x.DisputeReason, x.Description, x.TicketStatus, x.RefundAmount, x.ResolutionNotes,
                x.DisputeEvidenceRequests.Where(r => r.RequestedPartyUserId
                        == order.FarmerProfile.UserId)
                    .Select(r => new EvidenceRequestResponse(r.EvidenceRequestId,
                        r.RequestedAt, r.DueAt, r.RequestStatus, r.RespondedAt,
                        r.Instructions)).ToList())).ToList(), order.Order.VoucherCodeSnapshot);
    }
}




