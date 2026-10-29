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
        [RefundTransaction.StatusPending, RefundTransaction.StatusCompleted, "Success", "Succeeded"];

    private readonly AgriSustainContext _context;

    public ComplaintRepository(AgriSustainContext context)
    {
        _context = context;
    }

    public async Task<(int Total, IReadOnlyList<ComplaintListItemDto> Items)> SearchAsync(
        ComplaintQuery query,
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
                ticket.RefundAmount))
            .ToListAsync(cancellationToken);
        return (total, items);
    }

    public async Task<IReadOnlyList<string>> GetReasonsAsync(CancellationToken cancellationToken)
    {
        return await _context.DisputeTickets.AsNoTracking()
            .Select(ticket => ticket.DisputeReason)
            .Distinct()
            .OrderBy(reason => reason)
            .ToListAsync(cancellationToken);
    }

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
        string PartyOf(long userId) => userId == detail.BuyerUserId
            ? ComplaintParties.Buyer : ComplaintParties.Seller;
        return new ComplaintDetailDto(
            ticket.DisputeTicketId, ticket.DisputeReason, ticket.Description, ticket.TicketStatus,
            ticket.FaultParty, ticket.RefundAmount, ticket.ResolutionNotes, ticket.CreatedAt,
            ticket.ResolvedAt, detail.AssignedStaffName, detail.Complainant, detail.Buyer, detail.Seller,
            ticket.SellerOrderId, detail.SellerOrderCode, detail.SellerOrderStatus, detail.OrderCode,
            detail.OrderStatus, detail.SellerOrderAmount, detail.OrderFinalAmount, detail.DeliveredAt, detail.PaidAmount,
            detail.ReservedRefundAmount, 0,
            detail.Evidences.Select(evidence => new ComplaintEvidenceDto(
                evidence.DisputeEvidenceId, evidence.FullName, PartyOf(evidence.SubmittedByUserId),
                evidence.Description, evidence.SubmittedAt,
                evidence.FileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))).ToList(),
            detail.Requests.Select(request => new ComplaintEvidenceRequestDto(
                request.EvidenceRequestId, PartyOf(request.RequestedPartyUserId), request.Instructions,
                request.RequestedAt, request.DueAt, request.RequestStatus, request.RespondedAt,
                request.RespondedAt == null
                    && request.RequestStatus == DisputeTicket.EvidenceRequestPending
                    && (request.DueAt ?? request.RequestedAt.AddHours(48)) < now)).ToList(),
            detail.Refunds);
    }

    public Task<DisputeTicket?> GetForDecisionAsync(
        long ticketId,
        CancellationToken cancellationToken)
    {
        return _context.DisputeTickets
            .Include(ticket => ticket.DisputeEvidenceRequests)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.FarmerProfile)
            .Include(ticket => ticket.SellerOrder!)
                .ThenInclude(sellerOrder => sellerOrder.Order)
                    .ThenInclude(order => order.PaymentTransactions)
                        .ThenInclude(payment => payment.RefundTransactions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(ticket => ticket.DisputeTicketId == ticketId, cancellationToken);
    }

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

    public async Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        return await _context.Users.AsNoTracking()
            .Where(user => user.Status == "Active" && roles.Contains(user.Role))
            .Select(user => user.UserId)
            .ToListAsync(cancellationToken);
    }

    public void AddRefund(RefundTransaction refund)
    {
        _context.RefundTransactions.Add(refund);
    }

    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    public void AddNotification(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
