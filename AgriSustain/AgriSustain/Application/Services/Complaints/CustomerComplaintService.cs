using System.Security.Cryptography;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Complaints;

public sealed class CustomerComplaintService(
    ICustomerComplaintRepository repository,
    IComplaintRepository notifications,
    IFarmerComplaintFiles files,
    IComplaintEmailDelivery email,
    IUseCaseUnitOfWork unit,
    IPersistenceErrorClassifier errors,
    TimeProvider clock,
    ILogger<CustomerComplaintService> logger) : ICustomerComplaintService
{
    public const string ResponseAction = "CustomerComplaintResponse";

    private static readonly Error s_forbidden = new(
        "MSG121", "Bạn không có quyền truy cập khiếu nại hoặc yêu cầu này.", "Forbidden");
    private static readonly Error s_expired = new(
        "MSG166", FarmerComplaintMessages.Expired, "Conflict");
    private static readonly Error s_closed = new(
        "MSG184", FarmerComplaintMessages.Closed, "Conflict");
    private static readonly Error s_conflict = new(
        "MSG122", FarmerComplaintMessages.Conflict, "Conflict");
    private static readonly Error s_invalid = new("MSG183", FarmerComplaintMessages.Invalid);

    public async Task<Result<PagedResponse<CustomerComplaintListItem>>> ListAsync(
        long buyerId, CustomerComplaintQuery query, CancellationToken ct)
    {
        if (!CustomerComplaintValidator.IsValid(query))
        {
            return Result<PagedResponse<CustomerComplaintListItem>>.FailureResult(
                new Error("MSG183", "Bộ lọc hoặc số trang không hợp lệ."));
        }
        var page = await repository.SearchAsync(buyerId, query, clock.GetUtcNow().UtcDateTime, ct);
        return Result<PagedResponse<CustomerComplaintListItem>>.SuccessResult(page);
    }

    public async Task<Result<CustomerComplaintDetail>> GetAsync(
        long buyerId, long id, CancellationToken ct)
    {
        var ticket = await repository.LoadOwnedAsync(buyerId, id, forUpdate: false, ct);
        if (ticket?.SellerOrder is not { } sellerOrder)
        {
            return Result<CustomerComplaintDetail>.FailureResult(s_forbidden);
        }

        var audits = await repository.GetResponsesAsync(buyerId, id, ct);
        var responses = audits.Select(FarmerComplaintResponseText.Read).OfType<FarmerComplaintResponseText>()
            .ToLookup(response => response.RequestId);
        var evidence = ticket.DisputeEvidences.Where(file => file.SubmittedByUserId == buyerId)
            .OrderBy(file => file.DisputeEvidenceId).ToLookup(file => file.EvidenceRequestId);
        var now = clock.GetUtcNow().UtcDateTime;
        var requests = ticket.DisputeEvidenceRequests
            .Where(request => request.RequestedPartyUserId == buyerId)
            .OrderByDescending(request => request.RequestedAt)
            .ThenByDescending(request => request.EvidenceRequestId)
            .Select(request =>
            {
                var attachments = evidence[request.EvidenceRequestId];
                bool expired = request.RequestStatus == DisputeTicket.EvidenceRequestExpired
                    || (request.IsOpenFor(buyerId) && request.ResponseDeadline < now);
                bool canRespond = ticket.IsAwaitingDecision && request.IsOpenFor(buyerId) && !expired;
                return new CustomerComplaintRequest(
                    request.EvidenceRequestId, request.Instructions, Utc(request.RequestedAt),
                    Utc(request.ResponseDeadline),
                    request.RespondedAt is DateTime responded ? Utc(responded) : null,
                    request.RespondedAt.HasValue ? "Responded"
                        : expired ? "Expired" : canRespond ? "Pending" : "Closed",
                    canRespond,
                    responses[request.EvidenceRequestId].LastOrDefault()?.Explanation
                        ?? attachments.FirstOrDefault()?.Description,
                    attachments.Select(FileDto).ToArray());
            }).ToArray();
        string? refundStatus = ticket.RefundTransactions
            .OrderByDescending(refund => refund.RefundTransactionId).FirstOrDefault()?.RefundStatus;
        return Result<CustomerComplaintDetail>.SuccessResult(new(
            id, sellerOrder.SellerOrderCode, ticket.DisputeReason,
            ticket.Description, ticket.TicketStatus,
            ticket.ResolutionNotes, ticket.RefundAmount, refundStatus, requests,
            evidence[null].Select(FileDto).ToArray(), Version(ticket)));
    }

    public async Task<Result<CustomerComplaintResponse>> RespondAsync(
        long buyerId, long id, CustomerComplaintResponseRequest input, CancellationToken ct)
    {
        string explanation = input.Explanation?.Trim() ?? "";
        if (!Guid.TryParseExact(input.RequestKey, "D", out var key) || key == Guid.Empty
            || !CustomerComplaintValidator.IsValid(input, explanation)
            || input.EvidenceUrls is not { } urls)
        {
            return Failure(s_invalid);
        }
        string traceId = $"customer-response:{key:D}";
        var emails = new List<Notification>();
        try
        {
            await using var transaction = await unit.BeginAsync(ct);
            var ticket = await repository.LoadOwnedAsync(buyerId, id, forUpdate: true, ct);
            var request = ticket?.DisputeEvidenceRequests.SingleOrDefault(
                item => item.EvidenceRequestId == input.EvidenceRequestId);
            if (ticket is null || request?.RequestedPartyUserId != buyerId)
            {
                return Failure(s_forbidden);
            }
            if (await repository.HasRequestAsync(traceId, ct))
            {
                return Failure(s_conflict);
            }
            var now = clock.GetUtcNow().UtcDateTime;
            if (request.RequestStatus == DisputeTicket.EvidenceRequestExpired
                || (request.IsOpenFor(buyerId) && now > request.ResponseDeadline))
            {
                return Failure(s_expired);
            }
            if (!ticket.IsAwaitingDecision || !request.IsOpenFor(buyerId))
            {
                return Failure(s_closed);
            }
            if (!string.Equals(input.Version, Version(ticket), StringComparison.Ordinal))
            {
                return Failure(s_conflict);
            }
            foreach (string url in urls)
            {
                if (!await files.IsValidResponseEvidenceAsync(buyerId, url, ct))
                {
                    return Failure(s_invalid);
                }
            }

            var recipients = await notifications.GetActiveUserIdsByRolesAsync(
                ComplaintNotifications.SupportRoles, ct);
            if (ticket.AssignedStaffUserId is long assigned && recipients.Contains(assigned))
            {
                recipients = [assigned];
            }
            request.MarkAnswered(now);
            ticket.MarkUnderReviewWhenAnswered();
            var response = new FarmerComplaintResponseText(request.EvidenceRequestId, explanation, urls);
            repository.AddResponse(new AuditLog
            {
                EntityType = FarmerComplaintResponseText.Entity,
                EntityId = id,
                Action = ResponseAction,
                PerformedByUserId = buyerId,
                TraceId = traceId,
                OccurredAt = now,
                NewValue = JsonSerializer.Serialize(response)
            }, urls.Select(url => new DisputeEvidence
            {
                DisputeTicketId = id,
                EvidenceRequestId = request.EvidenceRequestId,
                SubmittedByUserId = buyerId,
                SubmittedAt = now,
                FileUrl = url,
                Description = explanation.Length <= 500 ? explanation : explanation[..499] + "…"
            }));
            string content = CustomerComplaintMessages.SupportContent(id, request.EvidenceRequestId, now);
            foreach (long recipient in recipients)
            {
                foreach (var notice in ComplaintNotifications.Create(
                    recipient, id, CustomerComplaintMessages.SupportTitle, content, now))
                {
                    notifications.AddNotification(notice);
                    if (notice.DeliveryChannel == "Email")
                    {
                        emails.Add(notice);
                    }
                }
            }
            await unit.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (errors.IsConflict(exception))
        {
            return Failure(s_conflict);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not save response for complaint {ComplaintId}", id);
            return Failure(new Error("MSG123", CustomerComplaintMessages.SaveFailed, "Error"));
        }

        foreach (var notice in emails)
        {
            try
            {
                await email.DeliverAsync(notice.NotificationId, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "Complaint notification {NotificationId} deferred", notice.NotificationId);
            }
        }
        return Result<CustomerComplaintResponse>.SuccessResult(new(CustomerComplaintMessages.Saved));
    }

    public async Task<string?> GetEvidencePathAsync(
        long buyerId, long id, long evidenceId, CancellationToken ct)
    {
        string? url = await repository.GetOwnedEvidenceUrlAsync(buyerId, id, evidenceId, ct);
        return url is null ? null : files.Resolve(buyerId, url);
    }

    private static CustomerComplaintFile FileDto(DisputeEvidence file) =>
        new(file.DisputeEvidenceId, Path.GetFileName(file.FileUrl));

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static Result<CustomerComplaintResponse> Failure(Error error) =>
        Result<CustomerComplaintResponse>.FailureResult(error);

    private static string Version(DisputeTicket ticket) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ticket.DisputeTicketId,
            ticket.TicketStatus,
            ticket.AssignedStaffUserId,
            Requests = ticket.DisputeEvidenceRequests.OrderBy(request => request.EvidenceRequestId)
                .Select(request => new
                {
                    request.EvidenceRequestId,
                    request.RequestStatus,
                    DueAt = request.ResponseDeadline.Ticks,
                    RespondedAt = request.RespondedAt?.Ticks
                })
        })));
}
