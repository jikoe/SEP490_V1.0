using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Complaints;

public sealed class FarmerComplaintService(
    IFarmerComplaintRepository repository,
    IUnitOfWork unit,
    IFarmerComplaintFiles files,
    INotificationGateway gateway,
    TimeProvider clock,
    ILogger<FarmerComplaintService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<FeatureResult<FarmerComplaintPage>> GetComplaintsAsync(
        long actor, FarmerComplaintQuery query, CancellationToken ct)
    {
        if (!await Eligible(actor, false, ct))
        {
            return Denied<FarmerComplaintPage>();
        }
        string[] statuses = ["Open", "UnderReview", "WaitingCustomerInfo", "RefundPending", "Rejected", "Closed"];
        string[] requests = ["Pending", "Responded", "Expired", "Closed"];
        if (query.Page is < 1 or > 100000 || (query.Keyword?.Length ?? 0) > 100
            || (!string.IsNullOrEmpty(query.Status) && !statuses.Contains(query.Status))
            || (!string.IsNullOrEmpty(query.RequestStatus) && !requests.Contains(query.RequestStatus)))
            return FeatureResult<FarmerComplaintPage>.Fail("VALIDATION", FarmerComplaintMessages.Invalid);
        return FeatureResult<FarmerComplaintPage>.Ok(await repository.SearchOwnedAsync(actor, query, Now, ct));
    }

    public async Task<FeatureResult<FarmerComplaintDetail>> GetComplaintAsync(long actor, long id, CancellationToken ct)
    {
        if (!await Eligible(actor, false, ct))
        {
            return Denied<FarmerComplaintDetail>();
        }
        var ticket = await repository.LoadOwnedContextAsync(actor, id, false, ct);
        return ticket is null ? Denied<FarmerComplaintDetail>() : FeatureResult<FarmerComplaintDetail>.Ok(await Map(actor, ticket, ct));
    }

    public async Task<FeatureResult<FarmerComplaintDetail>> SubmitResponseAsync(
        long actor, long id, FarmerComplaintResponseRequest input, CancellationToken ct)
    {
        if (!Guid.TryParseExact(input.RequestKey, "D", out _) || input.Version?.Length != 64)
            return FeatureResult<FarmerComplaintDetail>.Fail("VALIDATION", FarmerComplaintMessages.Invalid);
        await unit.BeginAsync(ct);
        try
        {
            if (!await Eligible(actor, true, ct))
            {
                return await Abort("FORBIDDEN", FarmerComplaintMessages.Forbidden);
            }
            var ticket = await repository.LoadOwnedContextAsync(actor, id, true, ct);
            if (ticket is null)
            {
                return await Abort("FORBIDDEN", FarmerComplaintMessages.Forbidden);
            }
            var request = ticket.DisputeEvidenceRequests.SingleOrDefault(x => x.EvidenceRequestId == input.EvidenceRequestId);
            if (request is null || request.RequestedPartyUserId != actor)
                return await Abort("FORBIDDEN", FarmerComplaintMessages.Forbidden);
            if (await repository.HasRequestAsync(input.RequestKey, ct))
                return await Abort("CONFLICT", FarmerComplaintMessages.Conflict);
            if (!ticket.IsAwaitingDecision || !request.IsOpenFor(actor))
                return await Abort("CONFLICT", FarmerComplaintMessages.Closed);
            DateTime now = Now;
            if (now > request.ResponseDeadline)
            {
                return await Abort("CONFLICT", FarmerComplaintMessages.Expired);
            }
            var before = await Map(actor, ticket, ct);
            if (input.Version != before.Version)
            {
                return await Abort("CONFLICT", FarmerComplaintMessages.Conflict);
            }
            string explanation = input.Explanation?.Trim() ?? "";
            var urls = input.EvidenceUrls;
            if (explanation.Length is < 15 or > 2000 || urls is null || urls.Length is < 1 or > 5
                || urls.Any(string.IsNullOrWhiteSpace) || urls.Distinct(StringComparer.Ordinal).Count() != urls.Length)
                return await Abort("VALIDATION", FarmerComplaintMessages.Invalid);
            foreach (string url in urls)
                if (!await files.IsValidOwnedAsync(actor, url, ct))
                    return await Abort("VALIDATION", FarmerComplaintMessages.Invalid);
            var recipients = await repository.GetSupportRecipientsAsync(ticket.AssignedStaffUserId, ct);
            if (recipients.Count == 0)
            {
                return await Abort("VALIDATION", FarmerComplaintMessages.SaveFailed);
            }
            var content = new FarmerComplaintResponseText(request.EvidenceRequestId, explanation, urls);
            request.MarkAnswered(now);
            repository.SaveResponseAsync(new AuditLog
            {
                PerformedByUserId = actor,
                EntityType = FarmerComplaintResponseText.Entity,
                EntityId = id,
                Action = FarmerComplaintResponseText.Action,
                NewValue = JsonSerializer.Serialize(content),
                TraceId = "farmer-response:" + input.RequestKey,
                OccurredAt = now
            }, urls.Select(url => new DisputeEvidence
            {
                DisputeTicketId = id,
                EvidenceRequestId = request.EvidenceRequestId,
                SubmittedByUserId = actor,
                FileUrl = url,
                // The complete immutable explanation is retained in the response audit.
                Description = explanation.Length <= 500 ? explanation : explanation[..400] + "… " + FarmerComplaintMessages.FullTextReference,
                SubmittedAt = now
            }));
            repository.QueueSupportNotifications(recipients.SelectMany(user => new[] { "InApp", "Email" }.Select(channel => new Notification
            {
                UserId = user,
                Title = FarmerComplaintMessages.SupportTitle,
                Content = FarmerComplaintMessages.SupportContent(id, request.EvidenceRequestId, now),
                NotificationType = "FarmerComplaintResponse",
                DeliveryChannel = channel,
                DeliveryStatus = channel == "InApp" ? "Sent" : "Pending",
                SentAt = now,
                RelatedRecordType = "DisputeTicket",
                RelatedRecordId = id,
                PayloadJson = JsonSerializer.Serialize(new { EventKey = input.RequestKey, RequestId = request.EvidenceRequestId })
            })));
            await unit.CommitAsync(ct);
        }
        catch
        {
            await unit.RollbackAsync(CancellationToken.None);
            throw;
        }
        // Notification delivery must never roll back or repeat an accepted response.
        try
        {
            await NotifySupportAsync(input.RequestKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Complaint response notification deferred ({Type}).", ex.GetType().Name);
        }
        var result = await GetComplaintAsync(actor, id, ct);
        return result.IsSuccess ? FeatureResult<FarmerComplaintDetail>.Ok(result.Value! with
        {
            Message = result.Value!.NotificationPending ? FarmerComplaintMessages.NotificationPending : FarmerComplaintMessages.Saved
        }) : result;
    }

    public async Task<bool> CanUploadAsync(long actor, CancellationToken ct) => await Eligible(actor, true, ct);

    public async Task<string?> GetEvidenceFileUrlAsync(long actor, long ticketId, long evidenceId, CancellationToken ct)
    {
        if (!await Eligible(actor, false, ct))
        {
            return null;
        }
        var ticket = await repository.LoadOwnedContextAsync(actor, ticketId, false, ct);
        var evidence = ticket?.DisputeEvidences.SingleOrDefault(x => x.DisputeEvidenceId == evidenceId);
        if (evidence is null || (evidence.SubmittedByUserId != actor && evidence.SubmittedByUserId != ticket!.SellerOrder!.Order.BuyerUserId))
            return null;
        return files.Resolve(evidence.SubmittedByUserId, evidence.FileUrl);
    }

    public async Task NotifySupportAsync(string? eventKey, CancellationToken ct)
    {
        foreach (long id in await repository.PendingNotificationsAsync(eventKey, Now, ct))
        {
            var row = await repository.ClaimNotificationAsync(id, Now, ct);
            if (row is null) continue;
            bool sent = false;
            try
            {
                using var payload = JsonDocument.Parse(row.PayloadJson ?? "{}");
                string key = payload.RootElement.GetProperty("EventKey").GetString()!;
                await gateway.SendEmailAsync(row.User.Email, row.Title, row.Content, key + ":" + row.UserId, ct);
                sent = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Complaint notification {Id} deferred ({Type}).", id, ex.GetType().Name);
            }
            await repository.RecordChannelResultAsync(row, sent, Now, ct);
        }
    }

    private async Task<bool> Eligible(long actor, bool submit, CancellationToken ct)
    {
        var farmer = await repository.GetFarmerAsync(actor, ct);
        return farmer?.User is { Status: "Active", Role: "Farmer" }
            && (!submit || farmer.VerificationStatus == "Approved");
    }

    private async Task<FeatureResult<FarmerComplaintDetail>> Abort(string code, string message)
    {
        await unit.RollbackAsync(CancellationToken.None);
        return FeatureResult<FarmerComplaintDetail>.Fail(code, message);
    }

    private static FeatureResult<T> Denied<T>() => FeatureResult<T>.Fail("FORBIDDEN", FarmerComplaintMessages.Forbidden);

    private async Task<FarmerComplaintDetail> Map(long actor, DisputeTicket ticket, CancellationToken ct)
    {
        var audits = await repository.GetResponsesAsync(ticket.DisputeTicketId, ct);
        var saved = audits.Select(a => (Audit: a, Response: FarmerComplaintResponseText.Read(a)))
            .Where(x => x.Response is not null).ToList();
        bool approved = ticket.SellerOrder!.FarmerProfile.VerificationStatus == "Approved";
        DateTime now = Now;
        FarmerComplaintFile FileDto(DisputeEvidence evidence) => new(evidence.DisputeEvidenceId,
            Path.GetFileName(evidence.FileUrl), $"/api/farmer/complaints/{ticket.DisputeTicketId}/evidence/{evidence.DisputeEvidenceId}", evidence.Description);
        var requests = ticket.DisputeEvidenceRequests.Where(r => r.RequestedPartyUserId == actor)
            .OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.EvidenceRequestId)
            .Select(r => new FarmerComplaintRequest(r.EvidenceRequestId, r.Instructions,
                r.RequestedAt, r.ResponseDeadline, r.RespondedAt,
                r.RespondedAt.HasValue ? "Responded" : r.RequestStatus == "Expired" || (r.IsOpenFor(actor) && now > r.ResponseDeadline)
                    ? "Expired" : r.IsOpenFor(actor) && ticket.IsAwaitingDecision ? "Pending" : "Closed",
                approved && ticket.IsAwaitingDecision && r.IsOpenFor(actor) && now <= r.ResponseDeadline)).ToList();
        var histories = requests.Where(r => r.RespondedAt.HasValue).Select(r =>
        {
            var response = saved.LastOrDefault(x => x.Response!.RequestId == r.Id).Response;
            var attachments = ticket.DisputeEvidences.Where(e => e.EvidenceRequestId == r.Id && e.SubmittedByUserId == actor).ToList();
            return new FarmerComplaintHistory(r.Id, response?.Explanation ?? attachments.FirstOrDefault()?.Description ?? "",
                r.RespondedAt!.Value, attachments.Select(FileDto).ToList());
        }).OrderByDescending(x => x.SubmittedAt).ToList();
        string rawVersion = JsonSerializer.Serialize(new
        {
            ticket.DisputeTicketId,
            ticket.TicketStatus,
            ticket.AssignedStaffUserId,
            Requests = requests.Select(x => new { x.Id, x.Status, Due = x.DueAt.Ticks, Responded = x.RespondedAt?.Ticks }),
            LastAudit = audits.LastOrDefault()?.AuditLogId
        });
        string version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawVersion)));
        string? refund = ticket.RefundTransactions.OrderByDescending(x => x.RefundTransactionId).FirstOrDefault()?.RefundStatus;
        return new FarmerComplaintDetail(ticket.DisputeTicketId, ticket.SellerOrder.SellerOrderCode,
            ticket.SellerOrder.Order.BuyerUser.FullName, ticket.DisputeReason, ticket.Description,
            ticket.TicketStatus, ticket.SellerOrder.DeliveredAt, ticket.SellerOrder.DeliveredAt?.AddHours(48),
            ticket.ResolutionNotes, ticket.RefundAmount, refund, requests,
            ticket.DisputeEvidences.Where(e => e.SubmittedByUserId == ticket.SellerOrder.Order.BuyerUserId).Select(FileDto).ToList(),
            histories, version, await repository.HasFailedNotificationsAsync(ticket.DisputeTicketId, ct));
    }
}
