using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Services.Refunds;

/// <summary>
/// UC-FIN-03 Process Refund. Finance transfers the money outside the platform (bank transfer, or a
/// refund made in the PayOS/VietQR merchant portal) and records the transfer reference here; the
/// refund becomes Completed, the complaint Closed and the seller order Refunded. A failed transfer
/// is only recorded: the request stays Pending so it can be retried.
/// </summary>
public sealed class RefundProcessingService(
    IRefundRepository repository,
    IActorContext actor,
    IValidator<CompleteRefundRequest> completeValidator,
    IValidator<RefundTransferFailedRequest> failureValidator,
    TimeProvider timeProvider) : IRefundProcessingService
{
    public const string RefundedStatus = "Refunded";
    public const string PartiallyRefundedStatus = "PartiallyRefunded";
    private const int MaximumPageSize = 100;

    private static readonly Error s_notFound = new(
        "REFUND_NOT_FOUND", "Không tìm thấy yêu cầu hoàn tiền.", "NotFound");

    private static readonly Error s_notPending = new(
        "REFUND_NOT_PENDING",
        "Yêu cầu hoàn tiền đã được xử lý trước đó. Vui lòng tải lại để xem trạng thái mới nhất.",
        "Conflict");

    public async Task<Result<RefundListDto>> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken)
    {
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

        if (!refund.IsPending)
        {
            return Result<RefundActionResponse>.FailureResult(s_notPending);
        }

        string reference = request.TransferReference.Trim();
        if (await repository.TransferReferenceExistsAsync(reference, cancellationToken))
        {
            const string message = "Mã giao dịch này đã được dùng cho một khoản hoàn tiền khác.";
            return Result<RefundActionResponse>.FailureResult(new Error(
                "TRANSFER_REFERENCE_USED", message, "Conflict",
                new Dictionary<string, string[]> { ["transferReference"] = [message] }));
        }

        var now = Now();
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
            NotifyRefunded(sellerOrder, refund);
        }

        AddAudit(refund, "CompleteRefund", RefundTransaction.StatusPending, new
        {
            refund.RefundStatus, request.RefundMethod, TransferReference = reference,
            PaymentStatus = refund.PaymentTransaction.PaymentStatus, PreviousPaymentStatus = previousPaymentStatus
        }, request.Note?.Trim(), now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            "Đã xác nhận hoàn tiền thành công và thông báo cho khách hàng."));
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

        if (!refund.IsPending)
        {
            return Result<RefundActionResponse>.FailureResult(s_notPending);
        }

        AddAudit(refund, "RefundTransferFailed", refund.RefundStatus,
            new { refund.RefundStatus }, request.Reason.Trim(), Now());
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            "Đã ghi nhận chuyển tiền thất bại. Yêu cầu hoàn tiền vẫn ở trạng thái chờ xử lý."));
    }

    /// <summary>
    /// Refunded once completed refunds cover everything the buyer paid for the order; otherwise
    /// PartiallyRefunded, so the seller is still paid the remainder.
    /// </summary>
    private void MarkRefunded(SellerOrder sellerOrder, RefundTransaction refund, DateTime now)
    {
        var order = sellerOrder.Order;
        decimal refundedTotal = order.PaymentTransactions
            .SelectMany(payment => payment.RefundTransactions)
            .Where(item => item.RefundStatus == RefundTransaction.StatusCompleted)
            .Sum(item => item.Amount);
        bool fullyRefunded = refundedTotal >= order.FinalAmount;
        string newStatus = fullyRefunded ? RefundedStatus : PartiallyRefundedStatus;

        string previous = sellerOrder.Status;
        sellerOrder.Status = newStatus;
        sellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
        {
            ChangedByUserId = actor.UserId,
            OldStatus = previous,
            NewStatus = newStatus,
            Reason = $"Đã hoàn {refund.Amount:N0} ₫ ({refund.RefundCode}); tổng đã hoàn "
                + $"{refundedTotal:N0}/{order.FinalAmount:N0} ₫.",
            OccurredAt = now
        });

        order.OrderStatus = order.SellerOrders.Any(item =>
            item.Status == ComplaintResolutionService.RefundPendingStatus)
            ? ComplaintResolutionService.RefundPendingStatus
            : newStatus;
    }

    private void NotifyRefunded(SellerOrder sellerOrder, RefundTransaction refund)
    {
        foreach (long userId in new[] { sellerOrder.Order.BuyerUserId, sellerOrder.FarmerProfile.UserId }
            .Distinct())
        {
            repository.AddNotification(new Notification
            {
                UserId = userId,
                Title = "Hoàn tiền thành công",
                Content = $"Khoản hoàn {refund.Amount:N0} ₫ cho đơn {sellerOrder.SellerOrderCode} "
                    + $"({refund.RefundCode}) đã được chuyển. Mã giao dịch: {refund.GatewayReference}.",
                NotificationType = "Refund",
                IsRead = false,
                SentAt = Now(),
                DeliveryChannel = "InApp",
                DeliveryStatus = "Sent",
                RelatedRecordType = "RefundTransaction",
                RelatedRecordId = refund.RefundTransactionId
            });
        }
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
