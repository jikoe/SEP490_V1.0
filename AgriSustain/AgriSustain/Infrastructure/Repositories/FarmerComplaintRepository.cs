using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class FarmerComplaintRepository(AgriSustainContext db) : IFarmerComplaintRepository
{
    private const string NoticeType = "FarmerComplaintResponse";
    private static readonly string[] SupportRoles = ["CustomerSupport", "Customer Support", "DisputeOfficer"];

    public Task<FarmerProfile?> GetFarmerAsync(long actor, CancellationToken ct) =>
        db.FarmerProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == actor, ct);

    public async Task<FarmerComplaintPage> SearchOwnedAsync(
        long actor, FarmerComplaintQuery query, DateTime now, CancellationToken ct)
    {
        var tickets = db.DisputeTickets.AsNoTracking().Where(x => x.SellerOrder != null
            && x.SellerOrder.FarmerProfile.UserId == actor);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string key = query.Keyword.Trim().TrimStart('#');
            bool numeric = long.TryParse(key, out long id);
            tickets = tickets.Where(x => (numeric && x.DisputeTicketId == id)
                || x.SellerOrder!.SellerOrderCode.Contains(key));
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            tickets = tickets.Where(x => x.TicketStatus == query.Status);
        }
        var projected = tickets.Select(x => new
        {
            Ticket = x,
            Request = x.DisputeEvidenceRequests.Where(r => r.RequestedPartyUserId == actor)
                .OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.EvidenceRequestId).FirstOrDefault()
        });
        if (query.RequestStatus == "Responded")
            projected = projected.Where(x => x.Request != null && x.Request.RespondedAt != null);
        else if (query.RequestStatus == "Pending")
            projected = projected.Where(x => x.Request != null && x.Request.RequestStatus == "Pending"
                && x.Request.RespondedAt == null && (x.Request.DueAt ?? x.Request.RequestedAt.AddHours(48)) >= now
                && (x.Ticket.TicketStatus == "Open" || x.Ticket.TicketStatus == "UnderReview" || x.Ticket.TicketStatus == "WaitingCustomerInfo"));
        else if (query.RequestStatus == "Expired")
            projected = projected.Where(x => x.Request != null && x.Request.RespondedAt == null
                && (x.Request.RequestStatus == "Expired" || (x.Request.RequestStatus == "Pending"
                    && (x.Request.DueAt ?? x.Request.RequestedAt.AddHours(48)) < now)));
        else if (query.RequestStatus == "Closed")
            projected = projected.Where(x => x.Request == null || x.Request.RequestStatus == "Closed"
                || (x.Request.RespondedAt == null && x.Request.RequestStatus != "Expired"
                    && x.Ticket.TicketStatus != "Open" && x.Ticket.TicketStatus != "UnderReview"
                    && x.Ticket.TicketStatus != "WaitingCustomerInfo"));
        int total = await projected.CountAsync(ct);
        var records = await projected.OrderByDescending(x => x.Ticket.CreatedAt)
            .ThenByDescending(x => x.Ticket.DisputeTicketId).Skip((query.Page - 1) * 10).Take(10)
            .Select(x => new
            {
                x.Ticket.DisputeTicketId,
                x.Ticket.SellerOrder!.SellerOrderCode,
                x.Ticket.DisputeReason,
                x.Ticket.TicketStatus,
                RequestStatus = x.Request == null ? "Closed" : x.Request.RequestStatus,
                RespondedAt = x.Request == null ? null : x.Request.RespondedAt,
                DueAt = x.Request == null ? (DateTime?)null : x.Request.DueAt ?? x.Request.RequestedAt.AddHours(48)
            }).ToListAsync(ct);
        return new FarmerComplaintPage(records.Select(x => new FarmerComplaintListItem(
            x.DisputeTicketId, x.SellerOrderCode, x.DisputeReason, x.TicketStatus,
            x.RespondedAt.HasValue ? "Responded" : x.RequestStatus == "Expired" || (x.RequestStatus == "Pending" && x.DueAt < now)
                ? "Expired" : x.RequestStatus == "Pending" && x.TicketStatus is "Open" or "UnderReview" or "WaitingCustomerInfo"
                    ? "Pending" : "Closed", x.DueAt)).ToList(), total, query.Page);
    }

    public Task<DisputeTicket?> LoadOwnedContextAsync(long actor, long id, bool forUpdate, CancellationToken ct)
    {
        // Same row lock also protects against a simultaneous support decision or timeout resolution.
        var tickets = forUpdate
            ? db.DisputeTickets.FromSqlInterpolated($"SELECT * FROM dbo.DisputeTicket WITH (UPDLOCK,HOLDLOCK) WHERE DisputeTicketId={id}")
            : db.DisputeTickets.Where(x => x.DisputeTicketId == id);
        return tickets.Where(x => x.SellerOrder != null && x.SellerOrder.FarmerProfile.UserId == actor)
            .Include(x => x.SellerOrder!).ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
            .Include(x => x.SellerOrder!).ThenInclude(x => x.Order).ThenInclude(x => x.BuyerUser)
            .Include(x => x.DisputeEvidenceRequests).Include(x => x.DisputeEvidences)
            .Include(x => x.RefundTransactions).AsSplitQuery().SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetResponsesAsync(long id, CancellationToken ct) =>
        await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == FarmerComplaintResponseText.Entity
            && x.EntityId == id && x.Action == FarmerComplaintResponseText.Action)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditLogId).ToListAsync(ct);

    public Task<bool> HasRequestAsync(string key, CancellationToken ct) =>
        db.AuditLogs.AnyAsync(x => x.TraceId == "farmer-response:" + key, ct);

    public async Task<IReadOnlyList<long>> GetSupportRecipientsAsync(long? assigned, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(x => x.Status == "Active" && SupportRoles.Contains(x.Role));
        if (assigned.HasValue && await users.AnyAsync(x => x.UserId == assigned, ct))
            return [assigned.Value];
        return await users.Select(x => x.UserId).ToListAsync(ct);
    }

    public Task<bool> HasFailedNotificationsAsync(long id, CancellationToken ct) =>
        db.Notifications.AnyAsync(x => x.NotificationType == NoticeType && x.RelatedRecordId == id
            && x.DeliveryStatus == "Failed", ct);

    public void SaveResponseAsync(AuditLog response, IEnumerable<DisputeEvidence> files)
    {
        db.AuditLogs.Add(response);
        db.DisputeEvidences.AddRange(files);
    }

    public void QueueSupportNotifications(IEnumerable<Notification> notifications) => db.Notifications.AddRange(notifications);

    public async Task<IReadOnlyList<long>> PendingNotificationsAsync(string? eventKey, DateTime now, CancellationToken ct)
    {
        var q = db.Notifications.AsNoTracking().Where(x => x.NotificationType == NoticeType
            && x.DeliveryChannel == "Email" && (x.DeliveryStatus == "Pending"
                || (x.DeliveryStatus == "Failed" && x.SentAt <= now.AddMinutes(-1))
                || (x.DeliveryStatus == "Sending" && x.SentAt < now.AddMinutes(-5))));
        if (eventKey != null) q = q.Where(x => x.PayloadJson != null && x.PayloadJson.Contains(eventKey));
        return await q.OrderBy(x => x.NotificationId).Select(x => x.NotificationId).Take(25).ToListAsync(ct);
    }

    public async Task<Notification?> ClaimNotificationAsync(long id, DateTime now, CancellationToken ct)
    {
        int claimed = await db.Notifications.Where(x => x.NotificationId == id && x.NotificationType == NoticeType
            && x.DeliveryChannel == "Email" && (x.DeliveryStatus == "Pending"
                || (x.DeliveryStatus == "Failed" && x.SentAt <= now.AddMinutes(-1))
                || (x.DeliveryStatus == "Sending" && x.SentAt < now.AddMinutes(-5))))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.DeliveryStatus, "Sending").SetProperty(x => x.SentAt, now), ct);
        return claimed == 0 ? null : await db.Notifications.Include(x => x.User).SingleAsync(x => x.NotificationId == id, ct);
    }

    public async Task RecordChannelResultAsync(Notification notification, bool sent, DateTime now, CancellationToken ct)
    {
        notification.DeliveryStatus = sent ? "Sent" : "Failed";
        notification.SentAt = now;
        if (!sent) notification.RetryCount++;
        await db.SaveChangesAsync(ct);
    }
}
