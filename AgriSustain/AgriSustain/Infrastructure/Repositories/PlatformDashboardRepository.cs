using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-ADM-01 read-only queries over orders, users and the pending queues (BR-DB-02, BR-DB-03).</summary>
public sealed class PlatformDashboardRepository(AgriSustainContext db) : IPlatformDashboardRepository
{
    private static readonly string[] s_kycPending =
        [VerificationStatuses.PendingApproval, "Pending", "PendingVerification"];
    private static readonly string[] s_deliveryFinal = ["Delivered", "DeliveryFailed", "Returned"];
    private static readonly string[] s_cancelled = ["Cancelled", "Rejected"];
    private static readonly string[] s_complaintOpen =
        [DisputeTicket.StatusOpen, DisputeTicket.StatusUnderReview, DisputeTicket.StatusWaitingInfo];
    private static readonly string[] s_refundOpen =
    [
        RefundTransaction.StatusPending, RefundTransaction.StatusProcessing, RefundTransaction.StatusFailed,
        RefundTransaction.StatusNeedsReconciliation
    ];

    public async Task<OrderStatsDto> OrderStatsAsync(DateTime from, DateTime until,
        CancellationToken cancellationToken)
    {
        // Cancellation rate: seller orders created in the period (BR-DB-02).
        var created = db.SellerOrders.AsNoTracking()
            .Where(order => order.Order.OrderDate >= from && order.Order.OrderDate < until);
        int total = await created.CountAsync(cancellationToken);
        int cancelled = await created.CountAsync(order => s_cancelled.Contains(order.Status), cancellationToken);

        // Delivery-success rate: seller orders whose first final delivery state falls in the period.
        var finals = await db.SellerOrders.AsNoTracking()
            .Select(order => order.OrderStatusHistories
                .Where(history => s_deliveryFinal.Contains(history.NewStatus))
                .OrderBy(history => history.OccurredAt).ThenBy(history => history.OrderStatusHistoryId)
                .Select(history => new { history.NewStatus, history.OccurredAt })
                .FirstOrDefault())
            .Where(first => first != null && first.OccurredAt >= from && first.OccurredAt < until)
            .Select(first => first!.NewStatus)
            .ToListAsync(cancellationToken);
        int finished = finals.Count;
        int delivered = finals.Count(status => status == "Delivered");
        return new OrderStatsDto(total, finished, delivered, Rate(delivered, finished), cancelled,
            Rate(cancelled, total));
    }

    public async Task<NewUsersDto> NewUsersAsync(DateTime from, DateTime until,
        CancellationToken cancellationToken)
    {
        var counts = await db.Users.AsNoTracking()
            .Where(user => user.CreatedAt >= from && user.CreatedAt < until
                && (user.Role == "RetailCustomer" || user.Role == "Farmer" || user.Role == "WholesaleBuyer"))
            .GroupBy(user => user.Role)
            .Select(group => new { Role = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Count(string role) => counts.FirstOrDefault(row => row.Role == role)?.Count ?? 0;
        return new NewUsersDto(Count("RetailCustomer"), Count("Farmer"), Count("WholesaleBuyer"));
    }

    public async Task<IReadOnlyList<PendingWorkRow>> PendingWorkAsync(CancellationToken cancellationToken)
    {
        var rows = new List<PendingWorkRow>();
        rows.AddRange(await KycAsync(null, cancellationToken));
        rows.AddRange(await ListingsAsync(null, cancellationToken));
        rows.AddRange(await ComplaintsAsync(null, cancellationToken));
        rows.AddRange(await RefundsAsync(null, cancellationToken));
        return rows;
    }

    public async Task<PendingWorkRow?> PendingWorkItemAsync(string type, long referenceId,
        CancellationToken cancellationToken)
    {
        var rows = type switch
        {
            "Kyc" => await KycAsync(referenceId, cancellationToken),
            "Listing" => await ListingsAsync(referenceId, cancellationToken),
            "Complaint" => await ComplaintsAsync(referenceId, cancellationToken),
            "Refund" => await RefundsAsync(referenceId, cancellationToken),
            _ => []
        };
        return rows.FirstOrDefault();
    }

    // KYC dossiers awaiting a Verification Officer (UC-VO-01). There is no separate submission
    // time, so the account creation time is used, as on the KYC review list.
    private async Task<List<PendingWorkRow>> KycAsync(long? userId, CancellationToken cancellationToken)
    {
        var farmers = await db.FarmerProfiles.AsNoTracking()
            .Where(profile => s_kycPending.Contains(profile.VerificationStatus)
                && (userId == null || profile.UserId == userId))
            .Select(profile => new PendingWorkRow("Kyc", profile.UserId,
                "Hồ sơ nông trại · " + profile.FarmName, profile.User.FullName, profile.User.CreatedAt,
                profile.VerificationStatus))
            .ToListAsync(cancellationToken);
        var buyers = await db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(profile => s_kycPending.Contains(profile.VerificationStatus)
                && (userId == null || profile.UserId == userId))
            .Select(profile => new PendingWorkRow("Kyc", profile.UserId,
                "Hồ sơ doanh nghiệp · " + profile.CompanyName, profile.User.FullName, profile.User.CreatedAt,
                profile.VerificationStatus))
            .ToListAsync(cancellationToken);
        return [.. farmers, .. buyers];
    }

    // Listings waiting for a Content Moderator (UC-CM-01).
    private Task<List<PendingWorkRow>> ListingsAsync(long? productId, CancellationToken cancellationToken)
    {
        return db.Products.AsNoTracking()
            .Where(product => product.ApprovalStatus == "Pending"
                && (productId == null || product.ProductId == productId))
            .Select(product => new PendingWorkRow("Listing", product.ProductId,
                "Sản phẩm · " + product.ProductName, product.FarmerProfile.User.FullName, product.CreatedAt,
                product.ApprovalStatus))
            .ToListAsync(cancellationToken);
    }

    // Complaints not yet decided by Customer Support (UC-CS-01).
    private Task<List<PendingWorkRow>> ComplaintsAsync(long? ticketId, CancellationToken cancellationToken)
    {
        return db.DisputeTickets.AsNoTracking()
            .Where(ticket => s_complaintOpen.Contains(ticket.TicketStatus)
                && (ticketId == null || ticket.DisputeTicketId == ticketId))
            .Select(ticket => new PendingWorkRow("Complaint", ticket.DisputeTicketId,
                "Khiếu nại · " + (ticket.SellerOrder != null ? ticket.SellerOrder.SellerOrderCode : "#" + ticket.DisputeTicketId),
                ticket.SubmittedByUser.FullName, ticket.CreatedAt, ticket.TicketStatus))
            .ToListAsync(cancellationToken);
    }

    // Refund requests Finance Staff have not finished transferring (UC-FIN-03). A complaint refund
    // waits from its decision; a Farmer-rejection refund from the seller order's last status change.
    private async Task<List<PendingWorkRow>> RefundsAsync(long? refundId, CancellationToken cancellationToken)
    {
        var refunds = await db.RefundTransactions.AsNoTracking()
            .Where(refund => s_refundOpen.Contains(refund.RefundStatus)
                && (refundId == null || refund.RefundTransactionId == refundId))
            .Select(refund => new
            {
                refund.RefundTransactionId,
                refund.RefundCode,
                refund.RefundStatus,
                refund.RequestKey,
                Buyer = refund.PaymentTransaction.Order != null
                    ? refund.PaymentTransaction.Order.BuyerUser.FullName : null,
                DecidedAt = refund.DisputeTicket != null ? refund.DisputeTicket.DecisionAt : null
            })
            .ToListAsync(cancellationToken);
        var sellerOrderIds = refunds
            .Select(refund => RefundAccountRepository.FarmerRejectedSellerOrderId(refund.RequestKey))
            .OfType<long>().Distinct().ToList();
        var rejectedAt = sellerOrderIds.Count == 0 ? new Dictionary<long, DateTime>()
            : await db.OrderStatusHistories.AsNoTracking()
                .Where(history => sellerOrderIds.Contains(history.SellerOrderId))
                .GroupBy(history => history.SellerOrderId)
                .Select(group => new { Id = group.Key, At = group.Max(history => history.OccurredAt) })
                .ToDictionaryAsync(row => row.Id, row => row.At, cancellationToken);
        return refunds.Select(refund =>
        {
            long? sellerOrderId = RefundAccountRepository.FarmerRejectedSellerOrderId(refund.RequestKey);
            DateTime? at = refund.DecidedAt
                ?? (sellerOrderId is long id && rejectedAt.TryGetValue(id, out var rejected) ? rejected : null);
            return new PendingWorkRow("Refund", refund.RefundTransactionId, "Hoàn tiền · " + refund.RefundCode,
                refund.Buyer ?? "—", at ?? DateTime.UtcNow, refund.RefundStatus);
        }).ToList();
    }

    private static decimal? Rate(int part, int whole) =>
        whole == 0 ? null : Math.Round(part * 100m / whole, 1);
}
