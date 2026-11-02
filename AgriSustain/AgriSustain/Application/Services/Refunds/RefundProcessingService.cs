using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Refunds;

/// <summary>
/// UC-FIN-03 Process Refund (BR-RF-01..07). Finance starts a transfer (Processing), pays the
/// customer-confirmed account outside the platform from the company bank app, then records
/// Succeeded with the bank transaction ID, Failed with a reason, or Needs Reconciliation when the
/// bank has not confirmed. Cancel Transfer returns Processing to Pending; Transfer Again restarts a
/// Failed request. Success closes the complaint and marks the seller order Refunded or Partially
/// Refunded; every step appends one history row (audit log).
/// </summary>
public sealed class RefundProcessingService(
    IRefundRepository repository,
    IActorContext actor,
    IValidator<CompleteRefundRequest> completeValidator,
    IValidator<RefundTransferFailedRequest> failureValidator,
    IValidator<RefundReconciliationRequest> reconciliationValidator,
    TimeProvider timeProvider) : IRefundProcessingService
{
    public const string RefundedStatus = SellerOrder.StatusRefunded;
    public const string PartiallyRefundedStatus = SellerOrder.StatusPartiallyRefunded;
    public const string EmailNotificationType = "RefundEmail";
    private const int MaximumPageSize = 100;

    private static readonly Error s_notFound = new(
        "REFUND_NOT_FOUND", "Không tìm thấy yêu cầu hoàn tiền.", "NotFound");

    // MSG-RF-04
    private static readonly Error s_stale = new(
        "REFUND_NOT_PENDING",
        "Yêu cầu hoàn tiền này đã được xử lý. Vui lòng tải lại để xem trạng thái hiện tại.",
        "Conflict");

    private static readonly Error s_noAccount = new(
        "REFUND_ACCOUNT_MISSING",
        "Yêu cầu đang chờ khách hàng cung cấp tài khoản nhận hoàn tiền. Chưa thể bắt đầu chuyển.",
        "Conflict");

    private static readonly Error s_balanceExceeded = new(
        "REFUND_BALANCE_EXCEEDED",
        "Số tiền hoàn vượt quá số dư còn hoàn được của đơn nông trại hoặc giao dịch thanh toán gốc.",
        "Conflict");

    public async Task<Result<RefundListDto>> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromDate is DateOnly from && query.ToDate is DateOnly to && from > to)
        {
            const string message = "Vui lòng sửa Ngày tạo: ngày bắt đầu không được sau ngày kết thúc.";
            return Result<RefundListDto>.FailureResult(new Error("INVALID_INPUT", message, "Validation",
                new Dictionary<string, string[]> { ["fromDate"] = [message] }));
        }

        var normalized = query with
        {
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            RefundMethod = string.IsNullOrWhiteSpace(query.RefundMethod) ? null : query.RefundMethod.Trim(),
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Page = Math.Max(query.Page, 1),
            PageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize)
        };
        var (total, items) = await repository.SearchAsync(normalized, cancellationToken);
        return Result<RefundListDto>.SuccessResult(
            new RefundListDto(items, total, normalized.Page, normalized.PageSize));
    }

    public async Task<Result<RefundDetailDto>> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
        var detail = await repository.GetDetailAsync(refundId, cancellationToken);
        return detail is null
            ? Result<RefundDetailDto>.FailureResult(s_notFound)
            : Result<RefundDetailDto>.SuccessResult(detail);
    }

    public async Task<Result<RefundActionResponse>> StartTransferAsync(
        long refundId,
        RefundTransferRequest request,
        CancellationToken cancellationToken)
    {
        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        if (IsStale(refund, request.ExpectedStatus)
            || refund.RefundStatus is not (RefundTransaction.StatusPending or RefundTransaction.StatusFailed)
            || refund.DisputeTicket is { } ticket && ticket.TicketStatus != DisputeTicket.StatusRefundPending)
        {
            return Result<RefundActionResponse>.FailureResult(s_stale);
        }

        if (!refund.HasReceivingAccount)
        {
            return Result<RefundActionResponse>.FailureResult(s_noAccount);
        }

        // Recheck both ceilings (BR-CS-04, DBR-46): the reserved totals include this refund.
        var sellerOrder = refund.DisputeTicket?.SellerOrder;
        if (refund.PaymentTransaction.ReservedRefundAmount > refund.PaymentTransaction.Amount
            || sellerOrder is not null && sellerOrder.ReservedRefundAmount > sellerOrder.FinalAmount)
        {
            return Result<RefundActionResponse>.FailureResult(s_balanceExceeded);
        }

        string previous = refund.RefundStatus;
        refund.StartTransfer();
        AddAudit(refund, previous == RefundTransaction.StatusFailed ? "TransferAgain" : "StartTransfer",
            previous, HistoryEntry(refund), null, Now());
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Đã bắt đầu chuyển khoản {refund.RefundCode}. Chuyển {refund.Amount:N0} ₫ với nội dung "
                + $"{refund.RefundCode}, sau đó ghi nhận kết quả."));
    }

    public async Task<Result<RefundActionResponse>> CancelTransferAsync(
        long refundId,
        RefundTransferRequest request,
        CancellationToken cancellationToken)
    {
        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        if (IsStale(refund, request.ExpectedStatus) || refund.RefundStatus != RefundTransaction.StatusProcessing)
        {
            return Result<RefundActionResponse>.FailureResult(s_stale);
        }

        refund.CancelTransfer();
        AddAudit(refund, "CancelTransfer", RefundTransaction.StatusProcessing, HistoryEntry(refund), null, Now());
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Đã huỷ lượt chuyển khoản {refund.RefundCode}. Yêu cầu trở lại trạng thái Chờ chuyển."));
    }

    public async Task<Result<RefundActionResponse>> CompleteAsync(
        long refundId,
        CompleteRefundRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(completeValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<RefundActionResponse>.FailureResult(error);
        }

        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        if (!refund.AwaitsTransferResult)
        {
            return Result<RefundActionResponse>.FailureResult(s_stale);
        }

        string reference = request.TransferReference.Trim();
        if (await repository.TransferReferenceExistsAsync(reference, cancellationToken))
        {
            // MSG-RF-03
            const string message = "Mã giao dịch ngân hàng này đã được dùng cho một khoản hoàn tiền khác.";
            return Result<RefundActionResponse>.FailureResult(new Error(
                "TRANSFER_REFERENCE_USED", message, "Conflict",
                new Dictionary<string, string[]> { ["transferReference"] = [message] }));
        }

        var now = Now();
        string previous = refund.RefundStatus;
        string previousPaymentStatus = refund.PaymentTransaction.PaymentStatus;
        refund.Complete(actor.UserId, request.RefundMethod, reference, now);
        var ticket = refund.DisputeTicket;
        var sellerOrder = ticket?.SellerOrder;
        if (ticket is not null && ticket.TicketStatus == DisputeTicket.StatusRefundPending)
        {
            ticket.CloseAfterRefund();
        }

        if (sellerOrder is not null)
        {
            MarkRefunded(sellerOrder, refund, now);
            NotifyRefunded(sellerOrder, refund, now);
        }

        AddAudit(refund, "CompleteRefund", previous, new
        {
            refund.RefundStatus, refund.Amount, Account = RefundTransaction.Mask(refund.ReceiverAccountNumber),
            request.RefundMethod, TransferReference = reference,
            PaymentStatus = refund.PaymentTransaction.PaymentStatus, PreviousPaymentStatus = previousPaymentStatus
        }, request.Note?.Trim(), now);
        await repository.SaveChangesAsync(cancellationToken);

        // MSG-RF-01
        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Hoàn tiền {refund.RefundCode} số tiền {refund.Amount:N0} ₫ đã thành công. "
                + "Đã thông báo cho khách hàng và nông dân."));
    }

    public async Task<Result<RefundActionResponse>> RecordTransferFailureAsync(
        long refundId,
        RefundTransferFailedRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(failureValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<RefundActionResponse>.FailureResult(error);
        }

        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        if (!refund.AwaitsTransferResult)
        {
            return Result<RefundActionResponse>.FailureResult(s_stale);
        }

        var now = Now();
        string previous = refund.RefundStatus;
        string reason = request.Reason.Trim();
        refund.Fail();
        if (refund.DisputeTicket?.SellerOrder is { } sellerOrder)
        {
            NotifyTransferFailed(sellerOrder.Order.BuyerUserId, refund, reason, now);
        }

        AddAudit(refund, "RefundTransferFailed", previous, HistoryEntry(refund), reason, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Đã ghi nhận chuyển khoản {refund.RefundCode} thất bại. Khách hàng đã được thông báo "
                + "kiểm tra tài khoản nhận hoàn tiền."));
    }

    public async Task<Result<RefundActionResponse>> MarkNeedsReconciliationAsync(
        long refundId,
        RefundReconciliationRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(reconciliationValidator, request, cancellationToken);
        if (error is not null)
        {
            return Result<RefundActionResponse>.FailureResult(error);
        }

        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        if (refund.RefundStatus != RefundTransaction.StatusProcessing)
        {
            return Result<RefundActionResponse>.FailureResult(s_stale);
        }

        refund.MarkNeedsReconciliation();
        AddAudit(refund, "NeedsReconciliation", RefundTransaction.StatusProcessing, HistoryEntry(refund),
            request.Note.Trim(), Now());
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Đã chuyển {refund.RefundCode} sang Cần đối soát. Kiểm tra sao kê ngân hàng trước khi "
                + "ghi nhận thành công hoặc thất bại."));
    }

    private static bool IsStale(RefundTransaction refund, string? expectedStatus) =>
        !string.IsNullOrWhiteSpace(expectedStatus) && expectedStatus.Trim() != refund.RefundStatus;

    /// <summary>The BR-RF-04 history fields; the account number is always masked.</summary>
    private static object HistoryEntry(RefundTransaction refund) => new
    {
        refund.RefundStatus, refund.Amount, Account = RefundTransaction.Mask(refund.ReceiverAccountNumber)
    };

    /// <summary>
    /// Refunded once completed refunds cover everything the buyer paid for this seller order;
    /// otherwise PartiallyRefunded, so the seller is still paid the remainder. The parent order
    /// status is then derived from all of its seller orders.
    /// </summary>
    private void MarkRefunded(SellerOrder sellerOrder, RefundTransaction refund, DateTime now)
    {
        var order = sellerOrder.Order;
        decimal refundedTotal = sellerOrder.CompletedRefundAmount;
        bool fullyRefunded = refundedTotal >= sellerOrder.FinalAmount;
        string newStatus = fullyRefunded ? RefundedStatus : PartiallyRefundedStatus;

        string previous = sellerOrder.Status;
        sellerOrder.Status = newStatus;
        sellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
        {
            ChangedByUserId = actor.UserId,
            OldStatus = previous,
            NewStatus = newStatus,
            Reason = $"Đã hoàn {refund.Amount:N0} ₫ ({refund.RefundCode}); tổng đã hoàn "
                + $"{refundedTotal:N0}/{sellerOrder.FinalAmount:N0} ₫.",
            OccurredAt = now
        });

        order.SyncRefundStatus();
    }

    /// <summary>BR-RF-07: buyer in-app and by email (MSG-RF-09); the Farmer in-app only.</summary>
    private void NotifyRefunded(SellerOrder sellerOrder, RefundTransaction refund, DateTime now)
    {
        long buyerId = sellerOrder.Order.BuyerUserId;
        foreach (long userId in new[] { buyerId, sellerOrder.FarmerProfile.UserId }.Distinct())
        {
            AddInApp(userId, refund, "Hoàn tiền thành công",
                $"Khoản hoàn {refund.Amount:N0} ₫ cho đơn {sellerOrder.SellerOrderCode} "
                    + $"({refund.RefundCode}) đã được chuyển. Mã giao dịch: {refund.GatewayReference}.", now);
        }

        AddEmail(buyerId, refund, "completed",
            $"Hoàn tiền {refund.RefundCode} đã hoàn tất",
            $"{refund.Amount:N0} ₫ đã được chuyển tới tài khoản có đuôi {Last4(refund)} lúc "
                + $"{now.AddHours(7):dd/MM/yyyy HH:mm}. Mã giao dịch ngân hàng: {refund.GatewayReference}.", now);
    }

    /// <summary>BR-RF-07: buyer in-app (MSG-RF-06) and by email (MSG-RF-08).</summary>
    private void NotifyTransferFailed(long buyerId, RefundTransaction refund, string reason, DateTime now)
    {
        AddInApp(buyerId, refund, "Hoàn tiền chưa chuyển được",
            $"Khoản hoàn {refund.RefundCode} chưa chuyển được. Vui lòng kiểm tra tài khoản nhận hoàn tiền.", now);
        AddEmail(buyerId, refund, "failed",
            $"Hoàn tiền {refund.RefundCode} chưa chuyển được",
            $"Giao dịch chuyển tới tài khoản có đuôi {Last4(refund)} không thành công: {reason}. "
                + "Vui lòng đăng nhập AgriSustain và kiểm tra tài khoản nhận hoàn tiền.", now);
    }

    private static string Last4(RefundTransaction refund) =>
        RefundTransaction.Mask(refund.ReceiverAccountNumber)?.TrimStart('*') ?? "----";

    private void AddInApp(long userId, RefundTransaction refund, string title, string content, DateTime now)
    {
        repository.AddNotification(new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = "Refund",
            IsRead = false,
            SentAt = now,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            RelatedRecordType = "RefundTransaction",
            RelatedRecordId = refund.RefundTransactionId
        });
    }

    /// <summary>Queued for the email worker, which retries only this channel (JOB-09).</summary>
    private void AddEmail(long userId, RefundTransaction refund, string eventName, string subject, string body,
        DateTime now)
    {
        repository.AddNotification(new Notification
        {
            UserId = userId,
            Title = subject,
            Content = body,
            NotificationType = EmailNotificationType,
            IsRead = false,
            SentAt = now,
            DeliveryChannel = "Email",
            DeliveryStatus = "Pending",
            RelatedRecordType = "RefundTransaction",
            RelatedRecordId = refund.RefundTransactionId,
            PayloadJson = $"refund:{refund.RefundTransactionId}:{eventName}:{now.Ticks}"
        });
    }

    private void AddAudit(RefundTransaction refund, string action, string oldValue, object newValue,
        string? reason, DateTime now)
    {
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = actor.UserId,
            EntityType = "RefundTransaction",
            EntityId = refund.RefundTransactionId,
            Action = action,
            OldValue = oldValue,
            NewValue = JsonSerializer.Serialize(newValue),
            Reason = reason,
            TraceId = actor.TraceId,
            OccurredAt = now
        });
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
