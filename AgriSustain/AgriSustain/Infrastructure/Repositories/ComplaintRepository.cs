using AgriSustain.Application.Services.Complaints;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class ComplaintRepository : IComplaintRepository
{
    private static readonly string[] s_capturedStatuses = ["Success", "Succeeded", "Paid", "Completed"];
    private static readonly string[] s_reservingRefundStatuses =
        [
            RefundTransaction.StatusPending, RefundTransaction.StatusProcessing, RefundTransaction.StatusCompleted,
            RefundTransaction.StatusFailed, RefundTransaction.StatusNeedsReconciliation, "Success", "Succeeded"
        ];

    private readonly AgriSustainContext _context;

    /// <summary>
    /// Receives the EF Core context of the current request.
    /// </summary>
    /// <param name="context">Database context.</param>
    public ComplaintRepository(AgriSustainContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Builds the list query: status and reason are exact matches; the keyword matches the complaint id (a leading #
    /// is ignored) or is contained in the seller-order or order code. Undecided complaints come first, then oldest
    /// first. Each row also carries: overdue flag (an unanswered request past its deadline), the earliest open deadline,
    /// and whether every request was answered. No tracking; one COUNT and one page query.
    /// </summary>
    /// <param name="query">Normalized filters and paging.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Total matches and the page.</returns>
    public async Task<(int Total, IReadOnlyList<ComplaintListItemDto> Items)> SearchAsync(
        ComplaintQuery query,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var tickets = _context.DisputeTickets.AsNoTracking();
        if (query.Status is not null)
        {
            tickets = tickets.Where(ticket => ticket.TicketStatus == query.Status);
        }

        if (query.Reason is not null)
        {
            tickets = tickets.Where(ticket => ticket.DisputeReason == query.Reason);
        }

        if (query.Search is not null)
        {
            string search = query.Search.TrimStart('#');
            bool isId = long.TryParse(search, out long ticketId);
            tickets = tickets.Where(ticket => (isId && ticket.DisputeTicketId == ticketId)
                || (ticket.SellerOrder != null
                    && (ticket.SellerOrder.SellerOrderCode.Contains(search)
                        || ticket.SellerOrder.Order.OrderCode.Contains(search))));
        }

        int total = await tickets.CountAsync(cancellationToken);
        // Same overdue rule as the evidence-timeout job: DueAt, or RequestedAt + 48 hours.
        DateTime requestedBefore = now.AddHours(-48);
        var items = await tickets
            .OrderBy(ticket => ticket.TicketStatus == DisputeTicket.StatusOpen
                || ticket.TicketStatus == DisputeTicket.StatusUnderReview
                || ticket.TicketStatus == DisputeTicket.StatusWaitingInfo ? 0 : 1)
            .ThenBy(ticket => ticket.CreatedAt)
            .ThenBy(ticket => ticket.DisputeTicketId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ticket => new ComplaintListItemDto(
                ticket.DisputeTicketId,
                ticket.SellerOrder == null ? null : ticket.SellerOrder.SellerOrderCode,
                ticket.SellerOrder == null ? null : ticket.SellerOrder.Order.OrderCode,
                ticket.SubmittedByUser.FullName,
                ticket.DisputeReason,
                ticket.TicketStatus,
                ticket.CreatedAt,
                ticket.RefundAmount,
                ticket.DisputeEvidenceRequests.Any(request =>
                    request.RequestStatus == DisputeTicket.EvidenceRequestPending
                    && request.RespondedAt == null
                    && (request.DueAt != null ? request.DueAt < now : request.RequestedAt < requestedBefore)),
                ticket.DisputeEvidenceRequests
                    .Where(request => request.RequestStatus == DisputeTicket.EvidenceRequestPending
                        && request.RespondedAt == null)
                    .Min(request => (DateTime?)(request.DueAt ?? request.RequestedAt.AddHours(48))),
                ticket.DisputeEvidenceRequests.Any()
                    && ticket.DisputeEvidenceRequests.All(request =>
                        request.RequestStatus != DisputeTicket.EvidenceRequestPending || request.RespondedAt != null)))
            .ToListAsync(cancellationToken);
        return (total, items);
    }

    /// <summary>
    /// SELECT DISTINCT DisputeReason, ordered.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Reason texts.</returns>
    public async Task<IReadOnlyList<string>> GetReasonsAsync(CancellationToken cancellationToken)
    {
        return await _context.DisputeTickets.AsNoTracking()
            .Select(ticket => ticket.DisputeReason)
            .Distinct()
            .OrderBy(reason => reason)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the complaint read model: parties, seller order and order references, delivered time, paid and reserved
    /// amounts, items with their paid value, evidence, evidence requests (with the overdue flag) and refunds.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail with MaxRefundAmount = 0, or null.</returns>
    public async Task<ComplaintDetailDto?> GetDetailAsync(
        long ticketId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var detail = await _context.DisputeTickets.AsNoTracking()
            .Where(ticket => ticket.DisputeTicketId == ticketId)
            .Select(ticket => new
            {
                Ticket = ticket,
                AssignedStaffName = ticket.AssignedStaffUser == null
                    ? null : ticket.AssignedStaffUser.FullName,
                Complainant = new ComplaintPartyDto(ticket.SubmittedByUserId,
                    ticket.SubmittedByUser.FullName, ticket.SubmittedByUser.Email,
                    ticket.SubmittedByUser.PhoneNumber),
                Buyer = ticket.SellerOrder == null ? null : new ComplaintPartyDto(
                    ticket.SellerOrder.Order.BuyerUserId, ticket.SellerOrder.Order.BuyerUser.FullName,
                    ticket.SellerOrder.Order.BuyerUser.Email, ticket.SellerOrder.Order.BuyerUser.PhoneNumber),
                Seller = ticket.SellerOrder == null ? null : new ComplaintPartyDto(
                    ticket.SellerOrder.FarmerProfile.UserId, ticket.SellerOrder.FarmerProfile.User.FullName,
                    ticket.SellerOrder.FarmerProfile.User.Email,
                    ticket.SellerOrder.FarmerProfile.User.PhoneNumber),
                SellerOrderCode = ticket.SellerOrder == null ? null : ticket.SellerOrder.SellerOrderCode,
                SellerOrderStatus = ticket.SellerOrder == null ? null : ticket.SellerOrder.Status,
                OrderCode = ticket.SellerOrder == null ? null : ticket.SellerOrder.Order.OrderCode,
                OrderStatus = ticket.SellerOrder == null ? null : ticket.SellerOrder.Order.OrderStatus,
                SellerOrderAmount = ticket.SellerOrder == null ? (decimal?)null : ticket.SellerOrder.FinalAmount,
                OrderFinalAmount = ticket.SellerOrder == null
                    ? (decimal?)null : ticket.SellerOrder.Order.FinalAmount,
                DeliveredAt = ticket.SellerOrder == null ? null : ticket.SellerOrder.DeliveredAt,
                PaidAmount = ticket.SellerOrder == null ? 0 : ticket.SellerOrder.Order.PaymentTransactions
                    .Where(payment => s_capturedStatuses.Contains(payment.PaymentStatus))
                    .Sum(payment => (decimal?)payment.Amount) ?? 0,
                ReservedRefundAmount = ticket.SellerOrder == null ? 0 : ticket.SellerOrder.Order
                    .PaymentTransactions
                    .Where(payment => s_capturedStatuses.Contains(payment.PaymentStatus))
                    .SelectMany(payment => payment.RefundTransactions)
                    .Where(refund => s_reservingRefundStatuses.Contains(refund.RefundStatus))
                    .Sum(refund => (decimal?)refund.Amount) ?? 0,
                SellerOrderReservedRefundAmount = _context.RefundTransactions
                    .Where(refund => refund.DisputeTicket != null
                        && refund.DisputeTicket.SellerOrderId == ticket.SellerOrderId
                        && s_reservingRefundStatuses.Contains(refund.RefundStatus))
                    .Sum(refund => (decimal?)refund.Amount) ?? 0,
                SellerOrderSubtotal = ticket.SellerOrder == null ? 0 : ticket.SellerOrder.Subtotal,
                SellerOrderDiscountShare = ticket.SellerOrder == null ? 0 : ticket.SellerOrder.DiscountShare,
                // A complaint without a seller order simply has no lines (LEFT JOIN).
                Items = ticket.SellerOrder!.OrderItems
                    .OrderBy(item => item.OrderItemId)
                    .Select(item => new
                    {
                        item.OrderItemId,
                        item.ProductNameSnapshot,
                        item.Quantity,
                        item.UnitSnapshot,
                        item.UnitPriceSnapshot,
                        item.SubTotal
                    }).ToList(),
                BuyerUserId = ticket.SellerOrder == null ? (long?)null : ticket.SellerOrder.Order.BuyerUserId,
                Evidences = ticket.DisputeEvidences
                    .OrderBy(evidence => evidence.SubmittedAt)
                    .Select(evidence => new
                    {
                        evidence.DisputeEvidenceId,
                        evidence.SubmittedByUserId,
                        evidence.SubmittedByUser.FullName,
                        evidence.Description,
                        evidence.SubmittedAt,
                        evidence.FileUrl
                    }).ToList(),
                Requests = ticket.DisputeEvidenceRequests
                    .OrderBy(request => request.RequestedAt)
                    .Select(request => new
                    {
                        request.EvidenceRequestId,
                        request.RequestedPartyUserId,
                        request.Instructions,
                        request.RequestedAt,
                        request.DueAt,
                        request.RequestStatus,
                        request.RespondedAt
                    }).ToList(),
                Refunds = ticket.RefundTransactions
                    .OrderBy(refund => refund.RefundTransactionId)
                    .Select(refund => new ComplaintRefundDto(
                        refund.RefundCode, refund.Amount, refund.RefundStatus, refund.ProcessedAt))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var ticket = detail.Ticket;
        var responseAudits = await _context.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == FarmerComplaintResponseText.Entity && x.EntityId == ticketId
                && x.Action == FarmerComplaintResponseText.Action)
            .OrderBy(x => x.AuditLogId).ToListAsync(cancellationToken);
        var responseTextByFile = responseAudits.Select(FarmerComplaintResponseText.Read)
            .Where(x => x != null).SelectMany(x => x!.EvidenceUrls.Select(url => (Url: url, x.Explanation)))
            .GroupBy(x => x.Url).ToDictionary(x => x.Key, x => x.Last().Explanation);
        string PartyOf(long userId) => userId == detail.BuyerUserId
            ? ComplaintParties.Buyer : ComplaintParties.Seller;
        return new ComplaintDetailDto(
            ticket.DisputeTicketId, ticket.DisputeReason, ticket.Description, ticket.TicketStatus,
            ticket.FaultParty, ticket.RefundAmount, ticket.ResolutionNotes, ticket.CreatedAt,
            ticket.ResolvedAt, detail.AssignedStaffName, detail.Complainant, detail.Buyer, detail.Seller,
            ticket.SellerOrderId, detail.SellerOrderCode, detail.SellerOrderStatus, detail.OrderCode,
            detail.OrderStatus, detail.SellerOrderAmount, detail.OrderFinalAmount, detail.DeliveredAt, detail.PaidAmount,
            detail.ReservedRefundAmount, detail.SellerOrderReservedRefundAmount, 0,
            detail.Items.Select(item => new ComplaintOrderItemDto(
                item.OrderItemId, item.ProductNameSnapshot, item.Quantity, item.UnitSnapshot,
                item.UnitPriceSnapshot,
                SellerOrder.PaidValue(item.SubTotal, detail.SellerOrderSubtotal,
                    detail.SellerOrderDiscountShare))).ToList(),
            detail.Evidences.Select(evidence => new ComplaintEvidenceDto(
                evidence.DisputeEvidenceId, evidence.FullName, PartyOf(evidence.SubmittedByUserId),
                responseTextByFile.TryGetValue(evidence.FileUrl, out var fullExplanation) ? fullExplanation : evidence.Description, evidence.SubmittedAt,
                evidence.FileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))).ToList(),
            detail.Requests.Select(request => new ComplaintEvidenceRequestDto(
                request.EvidenceRequestId, PartyOf(request.RequestedPartyUserId), request.Instructions,
                request.RequestedAt, request.DueAt, request.RequestStatus, request.RespondedAt,
                request.RespondedAt == null
                    && request.RequestStatus == DisputeTicket.EvidenceRequestPending
                    && (request.DueAt ?? request.RequestedAt.AddHours(48)) < now)).ToList(),
            detail.Refunds);
    }

    /// <summary>
    /// Loads the tracked entity graph a decision changes, so the service can modify it and save once.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Tracked complaint, or null.</returns>
    public Task<DisputeTicket?> GetForDecisionAsync(
        long ticketId,
        CancellationToken cancellationToken)
    {
        return _context.DisputeTickets
            .Include(ticket => ticket.DisputeEvidenceRequests)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.FarmerProfile)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.OrderItems)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.DisputeTickets)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.Order)
                    .ThenInclude(order => order.SellerOrders)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.Order)
                    .ThenInclude(order => order.PaymentTransactions)
                        .ThenInclude(payment => payment.RefundTransactions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ticket => ticket.DisputeTicketId == ticketId, cancellationToken);
    }

    /// <summary>
    /// Ids of WaitingCustomerInfo complaints of a seller order that have at least one open request and whose open
    /// requests are all overdue (DueAt earlier than now, or RequestedAt older than the window when DueAt is missing).
    /// </summary>
    /// <param name="now">Current UTC time.</param>
    /// <param name="responseWindow">Fallback window.</param>
    /// <param name="take">Batch size.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Ids ordered by the earliest deadline.</returns>
    public async Task<IReadOnlyList<long>> GetOverdueTicketIdsAsync(
        DateTime now,
        TimeSpan responseWindow,
        int take,
        CancellationToken cancellationToken)
    {
        // Requests without DueAt fall back to RequestedAt + window, compared as RequestedAt < now − window.
        DateTime requestedBefore = now - responseWindow;
        return await _context.DisputeTickets.AsNoTracking()
            .Where(ticket => ticket.TicketStatus == DisputeTicket.StatusWaitingInfo
                && ticket.SellerOrderId != null)
            .Select(ticket => new
            {
                ticket.DisputeTicketId,
                Open = ticket.DisputeEvidenceRequests.Where(request =>
                    request.RequestStatus == DisputeTicket.EvidenceRequestPending
                    && request.RespondedAt == null)
            })
            .Where(item => item.Open.Any()
                && item.Open.All(request => request.DueAt != null
                    ? request.DueAt < now
                    : request.RequestedAt < requestedBefore))
            .OrderBy(item => item.Open.Min(request => request.DueAt ?? request.RequestedAt))
            .Select(item => item.DisputeTicketId)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Reads FileUrl of the evidence only when it belongs to the complaint, so an id from another complaint returns null.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="evidenceId">Evidence id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>URL or null.</returns>
    public Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken)
    {
        return _context.DisputeEvidences.AsNoTracking()
            .Where(evidence => evidence.DisputeEvidenceId == evidenceId
                && evidence.DisputeTicketId == ticketId)
            .Select(evidence => evidence.FileUrl)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Active users whose role is in the list.
    /// </summary>
    /// <param name="roles">Role names.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>User ids.</returns>
    public async Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        return await _context.Users.AsNoTracking()
            .Where(user => user.Status == "Active" && roles.Contains(user.Role))
            .Select(user => user.UserId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The user's default bank account.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Account or null.</returns>
    public Task<UserBankAccount?> GetDefaultBankAccountAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        return _context.UserBankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(account => account.UserId == userId && account.IsDefault, cancellationToken);
    }

    /// <summary>
    /// Adds the refund to the context (inserted on save).
    /// </summary>
    /// <param name="refund">New refund.</param>
    public void AddRefund(RefundTransaction refund)
    {
        _context.RefundTransactions.Add(refund);
    }

    /// <summary>
    /// Adds the audit row to the context.
    /// </summary>
    /// <param name="audit">New audit row.</param>
    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    /// <summary>
    /// Adds the notification row to the context.
    /// </summary>
    /// <param name="notification">New notification.</param>
    public void AddNotification(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    /// <summary>
    /// Runs EF Core SaveChanges: all pending inserts and updates in one transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
