using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Complaints;

/// <summary>
/// UC-CS-01 Resolve Complaint (SRS UC-ADM-06). A refund decision creates a manual refund request
/// for Finance (UC-FIN-03) and moves the seller order to RefundPending; a rejection keeps it as it
/// is. Stock is never restored here: returned goods are restocked only after inspection.
/// </summary>
public sealed class ComplaintResolutionService(
    IComplaintRepository repository,
    IActorContext actor,
    IValidator<ResolveComplaintRequest> resolveValidator,
    IValidator<RequestComplaintInfoRequest> requestInfoValidator,
    TimeProvider timeProvider) : IComplaintResolutionService, IComplaintEvidenceTimeoutProcessor
{
    public const string RefundPendingStatus = SellerOrder.StatusRefundPending;
    public static readonly TimeSpan EvidenceResponseWindow = TimeSpan.FromHours(48);
    private const int MaximumPageSize = 100;
    private static readonly string[] s_financeRoles = ["FinanceStaff", "Finance Staff"];

    public async Task<Result<ComplaintListDto>> SearchAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query with
        {
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            Reason = string.IsNullOrWhiteSpace(query.Reason) ? null : query.Reason.Trim(),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize)
        };
        var (total, items) = await repository.SearchAsync(normalized, Now(), cancellationToken);
        var reasons = await repository.GetReasonsAsync(cancellationToken);
        return Result<ComplaintListDto>.SuccessResult(new ComplaintListDto(
            items, total, normalized.Page, normalized.PageSize, reasons));
    }

    public async Task<Result<ComplaintDetailDto>> GetDetailAsync(
        long ticketId,
        CancellationToken cancellationToken)
    {
        var detail = await repository.GetDetailAsync(ticketId, Now(), cancellationToken);
        if (detail is null)
        {
            return Result<ComplaintDetailDto>.FailureResult(s_notFound);
        }

        // BR-CS-04: at most what is still refundable for this seller order, and never more than
        // what remains on the captured payment.
        decimal sellerOrderRemaining = (detail.SellerOrderAmount ?? 0) - detail.SellerOrderReservedRefundAmount;
        decimal paymentRemaining = detail.PaidAmount - detail.ReservedRefundAmount;
        return Result<ComplaintDetailDto>.SuccessResult(detail with
        {
            MaxRefundAmount = decimal.Max(0, decimal.Min(sellerOrderRemaining, paymentRemaining))
        });
    }

    public async Task<Result<ComplaintActionResponse>> ResolveAsync(
        long ticketId,
        ResolveComplaintRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(resolveValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<ComplaintActionResponse>.FailureResult(error);
        }

        var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
        if (DecisionBlocker(ticket) is { } blocked)
        {
            return Result<ComplaintActionResponse>.FailureResult(blocked);
        }

        var applied = await ApplyDecisionAsync(ticket!, request, request.Reason.Trim(), actor.UserId,
            actor.UserId, "ResolveComplaint", Now(), cancellationToken);
        if (!applied.IsSuccess)
        {
            return Result<ComplaintActionResponse>.FailureResult(applied.Error!);
        }

        await repository.SaveChangesAsync(cancellationToken);
        var refund = applied.Value;
        return Result<ComplaintActionResponse>.SuccessResult(new ComplaintActionResponse(
            ticket.DisputeTicketId, ticket.TicketStatus, refund?.RefundCode,
            refund is null
                ? "Đã từ chối khiếu nại và thông báo cho các bên liên quan."
                : "Đã ghi nhận quyết định hoàn tiền và chuyển yêu cầu sang bộ phận Tài chính."));
    }

    public async Task<Result<ComplaintActionResponse>> RequestInformationAsync(
        long ticketId,
        RequestComplaintInfoRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(requestInfoValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<ComplaintActionResponse>.FailureResult(error);
        }

        var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
        if (DecisionBlocker(ticket) is { } blocked)
        {
            return Result<ComplaintActionResponse>.FailureResult(blocked);
        }

        var sellerOrder = ticket!.SellerOrder!;
        var now = Now();
        long partyUserId = request.Party == ComplaintParties.Buyer
            ? sellerOrder.Order.BuyerUserId
            : sellerOrder.FarmerProfile.UserId;
        if (ticket.HasOpenRequestFor(partyUserId, now))
        {
            return Result<ComplaintActionResponse>.FailureResult(new Error(
                "INFO_REQUEST_PENDING",
                "Bên này đang có yêu cầu bổ sung thông tin chưa phản hồi và chưa quá hạn.",
                "Conflict"));
        }

        string previousStatus = ticket.TicketStatus;
        string instructions = request.Instructions.Trim();
        var evidenceRequest = ticket.RequestInformation(
            partyUserId, instructions, actor.UserId, now, EvidenceResponseWindow);
        repository.AddNotification(Notice(partyUserId, "Yêu cầu bổ sung thông tin khiếu nại",
            $"Khiếu nại #{ticket.DisputeTicketId} của đơn {sellerOrder.SellerOrderCode} cần bổ sung "
                + $"thông tin trong 48 giờ (hạn {evidenceRequest.DueAt:dd/MM/yyyy HH:mm} UTC): {instructions}",
            ticket.DisputeTicketId));
        AddAudit(ticket, "RequestComplaintInformation", previousStatus,
            new { ticket.TicketStatus, request.Party, evidenceRequest.DueAt }, instructions,
            actor.UserId, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<ComplaintActionResponse>.SuccessResult(new ComplaintActionResponse(
            ticket.DisputeTicketId, ticket.TicketStatus, null,
            "Đã gửi yêu cầu bổ sung thông tin. Bên liên quan có 48 giờ để phản hồi."));
    }

    public Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken) =>
        repository.GetEvidenceFileUrlAsync(ticketId, evidenceId, cancellationToken);

    /// <summary>
    /// SRS UC-ADM-06: a party that ignores an information request for 48 hours loses the case
    /// (buyer → claim rejected, seller → full refund); if both were asked and neither answered,
    /// the claim is rejected and the order stays Delivered. Each decision is logged as Evidence Overdue.
    /// </summary>
    public async Task<int> ResolveOverdueAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        var ticketIds = await repository.GetOverdueTicketIdsAsync(
            now, EvidenceResponseWindow, OverdueBatchSize, cancellationToken);
        int resolved = 0;
        foreach (long ticketId in ticketIds)
        {
            var ticket = await repository.GetForDecisionAsync(ticketId, cancellationToken);
            if (ticket is null || DecisionBlocker(ticket) is not null
                || OverdueDecision(ticket, now) is not { } decision)
            {
                continue;
            }

            foreach (var request in decision.OverdueRequests)
            {
                request.RequestStatus = DisputeTicket.EvidenceRequestExpired;
            }

            var applied = await ApplyDecisionAsync(ticket, decision.Request, decision.Request.Reason,
                decision.StaffUserId, null, "AutoResolveComplaint", now, cancellationToken);
            if (applied.IsSuccess)
            {
                resolved++;
            }
            else
            {
                // Nothing can be refunded automatically (e.g. no captured payment): hand back to staff.
                // The expired requests are no longer picked up, so this is reported only once.
                await NotifySupportAsync(ticket,
                    $"Không thể tự động xử lý khiếu nại #{ticket.DisputeTicketId} quá hạn bổ sung thông tin: "
                        + $"{applied.Error!.Message} Vui lòng xử lý thủ công.", cancellationToken);
            }

            await repository.SaveChangesAsync(cancellationToken);
        }

        return resolved;
    }

    private const int OverdueBatchSize = 25;
    private const string EvidenceOverdue = "Evidence Overdue";
    private static readonly string[] s_supportRoles = ["CustomerSupport", "Customer Support"];

    private static readonly Error s_notFound = new(
        "COMPLAINT_NOT_FOUND", "Không tìm thấy khiếu nại.", "NotFound");

    private sealed record OverdueOutcome(
        ResolveComplaintRequest Request,
        long StaffUserId,
        IReadOnlyList<DisputeEvidenceRequest> OverdueRequests);

    private static OverdueOutcome? OverdueDecision(DisputeTicket ticket, DateTime now)
    {
        var open = ticket.DisputeEvidenceRequests
            .Where(request => request.RequestStatus == DisputeTicket.EvidenceRequestPending
                && request.RespondedAt == null)
            .ToList();
        bool IsOverdue(DisputeEvidenceRequest request) =>
            (request.DueAt ?? request.RequestedAt.Add(EvidenceResponseWindow)) < now;
        var overdue = open.Where(IsOverdue).ToList();
        // Wait until every party that was asked has answered or run out of time.
        if (overdue.Count == 0 || overdue.Count != open.Count)
        {
            return null;
        }

        var sellerOrder = ticket.SellerOrder!;
        bool buyerOverdue = overdue.Any(request =>
            request.RequestedPartyUserId == sellerOrder.Order.BuyerUserId);
        bool sellerOverdue = overdue.Any(request =>
            request.RequestedPartyUserId == sellerOrder.FarmerProfile.UserId);
        long staffUserId = overdue.OrderByDescending(request => request.RequestedAt).First().RequestedByUserId;
        var request = (buyerOverdue, sellerOverdue) switch
        {
            (true, true) => new ResolveComplaintRequest(ComplaintResolutions.RejectClaim,
                ComplaintParties.NotDetermined, null,
                $"{EvidenceOverdue}: cả người mua và người bán đều không phản hồi yêu cầu bổ sung thông tin "
                    + "trong 48 giờ; khiếu nại tự động bị từ chối."),
            (true, false) => new ResolveComplaintRequest(ComplaintResolutions.RejectClaim,
                ComplaintParties.Buyer, null,
                $"{EvidenceOverdue}: người mua không phản hồi yêu cầu bổ sung thông tin trong 48 giờ."),
            (false, true) => new ResolveComplaintRequest(ComplaintResolutions.FullRefund,
                ComplaintParties.Seller, null,
                $"{EvidenceOverdue}: người bán không phản hồi yêu cầu bổ sung thông tin trong 48 giờ; "
                    + "hoàn tiền toàn bộ cho người mua."),
            _ => null
        };
        return request is null ? null : new OverdueOutcome(request, staffUserId, overdue);
    }

    private async Task<Result<RefundTransaction?>> ApplyDecisionAsync(DisputeTicket ticket,
        ResolveComplaintRequest request, string reason, long staffUserId, long? auditUserId,
        string auditAction, DateTime now, CancellationToken cancellationToken)
    {
        var sellerOrder = ticket.SellerOrder!;
        string previousStatus = ticket.TicketStatus;
        RefundTransaction? refund = null;
        if (request.Resolution == ComplaintResolutions.RejectClaim)
        {
            ticket.RecordDecision(
                DisputeTicket.StatusRejected, request.FaultParty, null, reason, staffUserId, now);
        }
        else
        {
            var created = CreateRefund(ticket, sellerOrder, request, reason, staffUserId, now);
            if (!created.IsSuccess)
            {
                return Result<RefundTransaction?>.FailureResult(created.Error!);
            }

            refund = created.Value!;
            ticket.RecordDecision(DisputeTicket.StatusRefundPending, request.FaultParty,
                refund.Amount, reason, staffUserId, now);
            MoveToRefundPending(sellerOrder, reason, staffUserId, now);
            await NotifyFinanceAsync(sellerOrder, refund, cancellationToken);
        }

        NotifyParties(ticket, sellerOrder, DecisionMessage(request.Resolution, refund, reason));
        AddAudit(ticket, auditAction, previousStatus, new
        {
            ticket.TicketStatus, request.Resolution, request.FaultParty,
            RefundAmount = refund?.Amount, refund?.RefundCode, RefundItems = request.Items
        }, reason, auditUserId, now);
        return Result<RefundTransaction?>.SuccessResult(refund);
    }

    private async Task NotifySupportAsync(DisputeTicket ticket, string content,
        CancellationToken cancellationToken)
    {
        var supportUsers = await repository.GetActiveUserIdsByRolesAsync(s_supportRoles, cancellationToken);
        foreach (long userId in supportUsers)
        {
            repository.AddNotification(Notice(userId, "Khiếu nại cần xử lý thủ công", content,
                ticket.DisputeTicketId));
        }
    }

    private static Error? DecisionBlocker(DisputeTicket? ticket)
    {
        if (ticket is null)
        {
            return s_notFound;
        }

        if (!ticket.IsAwaitingDecision)
        {
            return new Error("COMPLAINT_ALREADY_DECIDED",
                "Khiếu nại đã được xử lý trước đó. Vui lòng tải lại để xem trạng thái mới nhất.",
                "Conflict");
        }

        return ticket.SellerOrder is null
            ? new Error("COMPLAINT_NOT_SUPPORTED",
                "Khiếu nại không gắn với đơn hàng bán lẻ nên không xử lý được tại màn hình này.",
                "Conflict")
            : null;
    }

    private Result<RefundTransaction> CreateRefund(DisputeTicket ticket, SellerOrder sellerOrder,
        ResolveComplaintRequest request, string reason, long staffUserId, DateTime now)
    {
        var order = sellerOrder.Order;
        var payment = order.PaymentTransactions
            .Where(candidate => candidate.IsCaptured)
            .OrderByDescending(candidate => candidate.RefundableAmount)
            .FirstOrDefault();
        // BR-CS-04: the cap is what the buyer paid for this seller order (after its share of
        // vouchers and points, including its delivery fee), less refunds already requested or
        // made for it, and never more than what remains on the captured payment.
        decimal limit = payment is null ? 0 : decimal.Min(
            decimal.Max(0, sellerOrder.FinalAmount - sellerOrder.ReservedRefundAmount),
            payment.RefundableAmount);
        if (payment is null || limit <= 0)
        {
            // MSG-CS-06
            return Result<RefundTransaction>.FailureResult(new Error("NO_REFUNDABLE_PAYMENT",
                "Đơn hàng của người bán này không còn khoản thanh toán thành công nào có thể hoàn. "
                    + "Không thể tạo yêu cầu hoàn tiền.", "Conflict"));
        }

        decimal amount;
        if (request.Resolution == ComplaintResolutions.FullRefund)
        {
            // Full refund returns everything still refundable on the seller order.
            amount = limit;
        }
        else
        {
            var calculated = PartialRefundAmount(sellerOrder, request.Items!);
            if (!calculated.IsSuccess)
            {
                return Result<RefundTransaction>.FailureResult(calculated.Error!);
            }

            amount = calculated.Value;
        }

        if (amount > limit)
        {
            // MSG-CS-04
            string message = $"Số tiền hoàn {amount:N0} ₫ vượt quá số tiền còn có thể hoàn cho đơn "
                + $"của người bán này ({limit:N0} ₫).";
            return Result<RefundTransaction>.FailureResult(new Error("REFUND_AMOUNT_EXCEEDED", message,
                Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
        }

        var refund = new RefundTransaction
        {
            PaymentTransaction = payment,
            DisputeTicket = ticket,
            ProcessedByUserId = staffUserId,
            RefundCode = $"RF-{ticket.DisputeTicketId}-{now:yyyyMMddHHmmss}",
            Amount = amount,
            Reason = reason.Length > 500 ? reason[..500] : reason,
            RefundStatus = RefundTransaction.StatusPending,
            // One refund request per complaint; the unique index rejects a concurrent duplicate.
            RequestKey = $"complaint:{ticket.DisputeTicketId}"
        };
        repository.AddRefund(refund);
        return Result<RefundTransaction>.SuccessResult(refund);
    }

    /// <summary>
    /// BR-CS-03: the refund is the paid value of the selected quantities; each line must belong to
    /// the seller order and no quantity may exceed what was delivered.
    /// </summary>
    private static Result<decimal> PartialRefundAmount(SellerOrder sellerOrder,
        IReadOnlyList<RefundItemRequest> items)
    {
        decimal amount = 0;
        foreach (var selected in items)
        {
            var item = sellerOrder.OrderItems.SingleOrDefault(line => line.OrderItemId == selected.OrderItemId);
            if (item is null || selected.Quantity > item.Quantity)
            {
                // MSG-CS-04
                string message = item is null
                    ? "Sản phẩm được chọn không thuộc đơn hàng của người bán này."
                    : $"Số lượng hoàn của {item.ProductNameSnapshot} không được vượt quá "
                        + $"{item.Quantity:0.###} {item.UnitSnapshot} đã giao.";
                return Result<decimal>.FailureResult(new Error("REFUND_ITEMS_INVALID", message,
                    Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
            }

            amount += sellerOrder.RefundValueOf(item, selected.Quantity);
        }

        if (amount <= 0)
        {
            const string message = "Số tiền hoàn tính từ các sản phẩm được chọn phải lớn hơn 0.";
            return Result<decimal>.FailureResult(new Error("REFUND_ITEMS_INVALID", message,
                Fields: new Dictionary<string, string[]> { ["items"] = [message] }));
        }

        return Result<decimal>.SuccessResult(amount);
    }

    private static void MoveToRefundPending(SellerOrder sellerOrder, string reason, long staffUserId,
        DateTime now)
    {
        string previous = sellerOrder.Status;
        sellerOrder.Status = RefundPendingStatus;
        sellerOrder.Order.SyncRefundStatus();
        sellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
        {
            ChangedByUserId = staffUserId,
            OldStatus = previous,
            NewStatus = RefundPendingStatus,
            Reason = Truncate($"Khiếu nại được chấp nhận hoàn tiền: {reason}", 500),
            OccurredAt = now
        });
    }

    private async Task NotifyFinanceAsync(SellerOrder sellerOrder, RefundTransaction refund,
        CancellationToken cancellationToken)
    {
        var financeUsers = await repository.GetActiveUserIdsByRolesAsync(s_financeRoles, cancellationToken);
        foreach (long userId in financeUsers)
        {
            repository.AddNotification(Notice(userId, "Yêu cầu hoàn tiền mới",
                $"Yêu cầu {refund.RefundCode} hoàn {refund.Amount:N0} ₫ cho đơn "
                    + $"{sellerOrder.SellerOrderCode} đang chờ xử lý.",
                refund.DisputeTicket?.DisputeTicketId ?? 0));
        }
    }

    private void NotifyParties(DisputeTicket ticket, SellerOrder sellerOrder, string content)
    {
        foreach (long userId in new[] { sellerOrder.Order.BuyerUserId, sellerOrder.FarmerProfile.UserId }
            .Distinct())
        {
            repository.AddNotification(Notice(userId, "Kết quả xử lý khiếu nại",
                $"Khiếu nại #{ticket.DisputeTicketId} của đơn {sellerOrder.SellerOrderCode}: {content}",
                ticket.DisputeTicketId));
        }
    }

    private static string DecisionMessage(string resolution, RefundTransaction? refund, string reason) =>
        resolution switch
        {
            ComplaintResolutions.FullRefund =>
                $"chấp nhận hoàn tiền toàn bộ {refund!.Amount:N0} ₫, đang chờ bộ phận Tài chính chuyển khoản. Lý do: {reason}",
            ComplaintResolutions.PartialRefund =>
                $"chấp nhận hoàn tiền một phần {refund!.Amount:N0} ₫, đang chờ bộ phận Tài chính chuyển khoản. Lý do: {reason}",
            _ => $"khiếu nại bị từ chối. Lý do: {reason}"
        };

    private Notification Notice(long userId, string title, string content, long ticketId) => new()
    {
        UserId = userId,
        Title = title,
        Content = content,
        NotificationType = "Complaint",
        IsRead = false,
        SentAt = Now(),
        DeliveryChannel = "InApp",
        DeliveryStatus = "Sent",
        RelatedRecordType = "DisputeTicket",
        RelatedRecordId = ticketId
    };

    private void AddAudit(DisputeTicket ticket, string action, string previousStatus, object newValue,
        string reason, long? performedByUserId, DateTime now)
    {
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = performedByUserId,
            EntityType = "DisputeTicket",
            EntityId = ticket.DisputeTicketId,
            Action = action,
            OldValue = previousStatus,
            NewValue = JsonSerializer.Serialize(newValue),
            Reason = Truncate(reason, 500),
            TraceId = actor.TraceId,
            OccurredAt = now
        });
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    private static string Truncate(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
