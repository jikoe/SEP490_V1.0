using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class CustomerComplaintRepository(AgriSustainContext db) : ICustomerComplaintRepository
{
    public async Task<PagedResponse<CustomerComplaintListItem>> SearchAsync(
        long buyerId, CustomerComplaintQuery query, DateTime now, CancellationToken ct)
    {
        const int pageSize = 10;
        var tickets = Owned(db.DisputeTickets.AsNoTracking(), buyerId);
        if (query.Status is { Length: > 0 })
        {
            tickets = tickets.Where(ticket => ticket.TicketStatus == query.Status);
        }
        if (query.OrderId is long orderId)
        {
            tickets = tickets.Where(ticket => ticket.SellerOrder!.OrderId == orderId);
        }
        int total = await tickets.CountAsync(ct);
        var items = await tickets.OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.DisputeTicketId)
            .Skip((query.Page - 1) * pageSize).Take(pageSize)
            .Select(ticket => new CustomerComplaintListItem(
                ticket.DisputeTicketId, ticket.SellerOrder!.SellerOrderCode, ticket.DisputeReason,
                ticket.TicketStatus,
                (ticket.TicketStatus == DisputeTicket.StatusOpen
                    || ticket.TicketStatus == DisputeTicket.StatusUnderReview
                    || ticket.TicketStatus == DisputeTicket.StatusWaitingInfo)
                && ticket.DisputeEvidenceRequests.Any(request =>
                    request.RequestedPartyUserId == buyerId
                    && request.RequestStatus == DisputeTicket.EvidenceRequestPending
                    && request.RespondedAt == null
                    && (request.DueAt ?? request.RequestedAt.AddHours(48)) >= now)))
            .ToListAsync(ct);
        return new(items, total, query.Page, pageSize);
    }

    public Task<DisputeTicket?> LoadOwnedAsync(
        long buyerId, long id, bool forUpdate, CancellationToken ct)
    {
        var tickets = forUpdate
            ? db.DisputeTickets.FromSqlInterpolated(
                $"SELECT * FROM dbo.DisputeTicket WITH (UPDLOCK,HOLDLOCK) WHERE DisputeTicketId={id}")
            : db.DisputeTickets.AsNoTracking().Where(ticket => ticket.DisputeTicketId == id);
        return Owned(tickets, buyerId)
            .Include(ticket => ticket.SellerOrder)
            .Include(ticket => ticket.DisputeEvidenceRequests)
            .Include(ticket => ticket.DisputeEvidences.Where(e => e.SubmittedByUserId == buyerId))
            .Include(ticket => ticket.RefundTransactions)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetResponsesAsync(
        long buyerId, long id, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking()
            .Where(audit => audit.EntityType == FarmerComplaintResponseText.Entity
                && audit.EntityId == id && audit.PerformedByUserId == buyerId
                && audit.Action == CustomerComplaintService.ResponseAction)
            .OrderBy(audit => audit.AuditLogId).ToListAsync(ct);

    public Task<bool> HasRequestAsync(string traceId, CancellationToken ct) =>
        db.AuditLogs.AnyAsync(audit =>
            audit.Action == CustomerComplaintService.ResponseAction && audit.TraceId == traceId, ct);

    public Task<string?> GetOwnedEvidenceUrlAsync(
        long buyerId, long id, long evidenceId, CancellationToken ct) =>
        Owned(db.DisputeTickets.AsNoTracking(), buyerId)
            .Where(ticket => ticket.DisputeTicketId == id)
            .SelectMany(ticket => ticket.DisputeEvidences)
            .Where(evidence => evidence.DisputeEvidenceId == evidenceId
                && evidence.SubmittedByUserId == buyerId)
            .Select(evidence => evidence.FileUrl)
            .SingleOrDefaultAsync(ct);

    public void AddResponse(AuditLog audit, IEnumerable<DisputeEvidence> evidence)
    {
        db.AuditLogs.Add(audit);
        db.DisputeEvidences.AddRange(evidence);
    }

    private static IQueryable<DisputeTicket> Owned(IQueryable<DisputeTicket> tickets, long buyerId) =>
        tickets.Where(ticket => ticket.SellerOrder != null
            && ticket.SellerOrder.Order.OrderType == "Retail"
            && ticket.SellerOrder.Order.BuyerUserId == buyerId);
}
