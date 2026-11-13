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
    // Audit actions that (re)submit a listing for review (FarmerProductService).
    private static readonly string[] s_listingSubmissions = ["SubmitListing", "ResubmitListing", "UpdateListing"];
    private const string KycSubmission = "SubmitForVerification";
    private static readonly string[] s_refundOpen =
    [
        RefundTransaction.StatusPending, RefundTransaction.StatusProcessing, RefundTransaction.StatusFailed,
        RefundTransaction.StatusNeedsReconciliation
    ];

    /// <summary>
    /// Cancellation rate: seller orders whose parent order was placed in the period, Cancelled or Rejected among them.
    /// Delivery-success rate: seller orders whose first final delivery status (Delivered, DeliveryFailed, Returned)
    /// happened in the period, Delivered among them. Untracked queries.
    /// </summary>
    /// <param name="from">Start of the period (UTC, inclusive).</param>
    /// <param name="until">End of the period (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts and rates.</returns>
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

    /// <summary>
    /// Counts Retail Customer, Farmer and Wholesale Buyer accounts created in the period, grouped by role.
    /// </summary>
    /// <param name="from">Start of the period (UTC, inclusive).</param>
    /// <param name="until">End of the period (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts per role.</returns>
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

    /// <summary>
    /// Concatenates the four queues: KYC, listings, complaints and refunds.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>All pending rows.</returns>
    public async Task<IReadOnlyList<PendingWorkRow>> PendingWorkAsync(CancellationToken cancellationToken)
    {
        var rows = new List<PendingWorkRow>();
        rows.AddRange(await KycAsync(null, cancellationToken));
        rows.AddRange(await ListingsAsync(null, cancellationToken));
        rows.AddRange(await ComplaintsAsync(null, cancellationToken));
        rows.AddRange(await RefundsAsync(null, cancellationToken));
        return rows;
    }

    /// <summary>
    /// Runs the query of the given type for one id.
    /// </summary>
    /// <param name="type">Kyc, Listing, Complaint or Refund.</param>
    /// <param name="referenceId">Item id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The row when still pending, else null.</returns>
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

    /// <summary>
    /// KYC dossiers awaiting a Verification Officer (UC-VO-01), for Farmers and Wholesale Buyers. BR-DB-03: the submission
    /// time is the latest "SubmitForVerification" audit of the profile; the account creation time is only used for older
    /// data without that audit.
    /// </summary>
    /// <param name="userId">Limit to this one item; null for the whole queue.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Pending KYC rows.</returns>
    private async Task<List<PendingWorkRow>> KycAsync(long? userId, CancellationToken cancellationToken)
    {
        var farmers = await db.FarmerProfiles.AsNoTracking()
            .Where(profile => s_kycPending.Contains(profile.VerificationStatus)
                && (userId == null || profile.UserId == userId))
            .Select(profile => new PendingWorkRow("Kyc", profile.UserId,
                "Hồ sơ nông trại · " + profile.FarmName, profile.User.FullName,
                db.AuditLogs.Where(log => log.EntityType == "FarmerProfile"
                        && log.EntityId == profile.FarmerProfileId && log.Action == KycSubmission)
                    .Max(log => (DateTime?)log.OccurredAt) ?? profile.User.CreatedAt,
                profile.VerificationStatus))
            .ToListAsync(cancellationToken);
        var buyers = await db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(profile => s_kycPending.Contains(profile.VerificationStatus)
                && (userId == null || profile.UserId == userId))
            .Select(profile => new PendingWorkRow("Kyc", profile.UserId,
                "Hồ sơ doanh nghiệp · " + profile.CompanyName, profile.User.FullName,
                db.AuditLogs.Where(log => log.EntityType == "WholesaleBuyerProfile"
                        && log.EntityId == profile.WholesaleBuyerProfileId && log.Action == KycSubmission)
                    .Max(log => (DateTime?)log.OccurredAt) ?? profile.User.CreatedAt,
                profile.VerificationStatus))
            .ToListAsync(cancellationToken);
        return [.. farmers, .. buyers];
    }

    /// <summary>
    /// Listings waiting for a Content Moderator (UC-CM-01). BR-DB-03: submitted at the latest
    /// submit / resubmit / reviewed-update audit, so a resubmitted listing starts a new wait.
    /// </summary>
    /// <param name="productId">Limit to this one item; null for the whole queue.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Pending listing rows.</returns>
    private Task<List<PendingWorkRow>> ListingsAsync(long? productId, CancellationToken cancellationToken)
    {
        return db.Products.AsNoTracking()
            .Where(product => product.ApprovalStatus == "Pending"
                && (productId == null || product.ProductId == productId))
            .Select(product => new PendingWorkRow("Listing", product.ProductId,
                "Sản phẩm · " + product.ProductName, product.FarmerProfile.User.FullName,
                db.AuditLogs.Where(log => log.EntityType == "Product" && log.EntityId == product.ProductId
                        && s_listingSubmissions.Contains(log.Action))
                    .Max(log => (DateTime?)log.OccurredAt) ?? product.CreatedAt,
                product.ApprovalStatus))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Complaints not yet decided by Customer Support (UC-CS-01): Open, UnderReview or WaitingCustomerInfo, waiting since
    /// they were created.
    /// </summary>
    /// <param name="ticketId">Limit to this one item; null for the whole queue.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Pending complaint rows.</returns>
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

    /// <summary>
    /// Refund requests Finance Staff have not finished transferring (UC-FIN-03): Pending, Processing, Failed or
    /// NeedsReconciliation. A complaint refund waits from its decision; a Farmer-rejection refund from the seller
    /// order's last status change; otherwise from its first refund audit or the payment time.
    /// </summary>
    /// <param name="refundId">Limit to this one item; null for the whole queue.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Pending refund rows.</returns>
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
                DecidedAt = refund.DisputeTicket != null ? refund.DisputeTicket.DecisionAt : null,
                // Fallback when no decision / rejection time is found: when the request was created
                // (its first refund audit), else when the original payment was made.
                CreatedAt = db.AuditLogs.Where(log => log.EntityType == "RefundTransaction"
                        && log.EntityId == refund.RefundTransactionId)
                    .Min(log => (DateTime?)log.OccurredAt) ?? refund.PaymentTransaction.PaidAt
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
                refund.Buyer ?? "—", at ?? refund.CreatedAt ?? DateTime.UtcNow, refund.RefundStatus);
        }).ToList();
    }

    /// <summary>
    /// Percentage rounded to 1 decimal, or null when the divisor is 0 (BR-DB-02: never shown as 0 %).
    /// </summary>
    /// <param name="part">Numerator.</param>
    /// <param name="whole">Divisor.</param>
    /// <returns>Rate or null.</returns>
    private static decimal? Rate(int part, int whole) =>
        whole == 0 ? null : Math.Round(part * 100m / whole, 1);
}
