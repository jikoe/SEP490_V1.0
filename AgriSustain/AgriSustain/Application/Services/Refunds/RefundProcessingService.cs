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
/// Failed request. A complaint refund closes the complaint and marks the seller order Refunded or
/// Partially Refunded; a Farmer-rejection refund leaves the seller order Cancelled. Every step
/// appends one history row (audit log).
/// </summary>
public sealed class RefundProcessingService(
    IRefundRepository repository,
    IActorContext actor,
    IValidator<CompleteRefundRequest> completeValidator,
    IValidator<RefundTransferFailedRequest> failureValidator,
    IValidator<RefundReconciliationRequest> reconciliationValidator,
    TimeProvider timeProvider,
    IOrderFeeService? fees = null) : IRefundProcessingService
{
    /// <summary>
    /// Seller-order status once completed refunds cover its whole paid amount.
    /// </summary>
    public const string RefundedStatus = SellerOrder.StatusRefunded;
    /// <summary>
    /// Seller-order status while only part of its paid amount was refunded.
    /// </summary>
    public const string PartiallyRefundedStatus = SellerOrder.StatusPartiallyRefunded;
    /// <summary>
    /// Notification type of refund emails; the email worker (JOB-09) sends and retries this type.
    /// </summary>
    public const string EmailNotificationType = "RefundEmail";
    private const int MaximumPageSize = 100;
    /// <summary>
    /// History action written when a transfer fails; also read back to know when the last failure happened.
    /// </summary>
    public const string TransferFailedAction = RefundProcessingActions.TransferFailed;
    /// <summary>
    /// History action written for each reminder; read back to enforce one reminder per 24 hours.
    /// </summary>
    public const string AccountReminderAction = RefundProcessingActions.AccountReminder;
    /// <summary>
    /// MSG123 for UC-FIN-03 Step 9: shown by FeatureExceptionMiddleware when a refund action cannot be saved.
    /// SaveChangesAsync writes all or nothing, so the request keeps its previous status.
    /// </summary>
    public const string SaveFailedMessage =
        "Không thể lưu kết quả hoàn tiền. Vui lòng tải lại để kiểm tra thông tin mới nhất trước khi thử lại.";
    /// <summary>One reminder per request every 24 hours, like the B2B payment reminder.</summary>
    public static readonly TimeSpan AccountReminderInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Constructor kept for older callers and tests that do not pass a reconciliation validator or the fee service;
    /// it supplies the default reconciliation validator and no fee reversal.
    /// </summary>
    /// <param name="repository">Refund data access.</param>
    /// <param name="actor">Current Finance Staff user and trace id.</param>
    /// <param name="completeValidator">Rules of Record Success.</param>
    /// <param name="failureValidator">Rules of Record Failure.</param>
    /// <param name="timeProvider">Clock, replaceable in tests.</param>
    public RefundProcessingService(
        IRefundRepository repository,
        IActorContext actor,
        IValidator<CompleteRefundRequest> completeValidator,
        IValidator<RefundTransferFailedRequest> failureValidator,
        TimeProvider timeProvider)
        : this(repository, actor, completeValidator, failureValidator, new AgriSustain.Application.Validators.Refunds.RefundReconciliationRequestValidator(), timeProvider)
    {
    }

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

    private static readonly Error s_accountNotRechecked = new(
        "REFUND_ACCOUNT_NOT_RECHECKED",
        "Khách hàng chưa kiểm tra lại tài khoản nhận hoàn tiền sau lần chuyển thất bại. "
            + "Hãy nhắc khách hàng và chờ khách xác nhận trước khi chuyển lại.",
        "Conflict");

    private static readonly Error s_accountNotNeeded = new(
        "REFUND_ACCOUNT_NOT_NEEDED",
        "Yêu cầu này không còn chờ khách hàng cung cấp hoặc kiểm tra tài khoản nhận hoàn tiền.",
        "Conflict");

    private static readonly Error s_balanceExceeded = new(
        "REFUND_BALANCE_EXCEEDED",
        "Số tiền hoàn vượt quá số dư còn hoàn được của đơn nông trại hoặc giao dịch thanh toán gốc.",
        "Conflict");

    /// <summary>
    /// Screen (1)–(5). Refuses FromDate after ToDate, then trims the filters, turns blanks into "no filter",
    /// forces Page ≥ 1 and PageSize into 1..100 and returns the page. Read-only.
    /// </summary>
    /// <param name="query">Raw filters and paging.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Items, total, effective page and page size; or INVALID_INPUT on the fromDate field.</returns>
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

    /// <summary>
    /// Screen (6)–(17). Returns the read model built by the repository. Read-only.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or 404 REFUND_NOT_FOUND.</returns>
    public async Task<Result<RefundDetailDto>> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
        var detail = await repository.GetDetailAsync(refundId, cancellationToken);
        return detail is null
            ? Result<RefundDetailDto>.FailureResult(s_notFound)
            : Result<RefundDetailDto>.SuccessResult(detail);
    }

    /// <summary>
    /// Button (16). Checks, in order: 1) the refund exists; 2) it is not stale — status equals the one the screen
    /// showed, is Pending or Failed, and a complaint source is still RefundPending (MSG-RF-04); 3) a receiving account
    /// exists (otherwise Finance must wait for UC-FIN-08); 4) for Transfer Again, the customer confirmed the account
    /// after the last failure; 5) the reserved refunds still fit the payment and the seller order (BR-CS-04).
    /// Then sets Processing, writes the StartTransfer or TransferAgain history row and saves. No money moves here.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">ExpectedStatus from the screen.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Message telling Finance to transfer the amount with the RefundCode as content, or the blocking error.</returns>
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

        // UC-FIN-03 AF: Transfer Again only after the customer checked the account again.
        if (!refund.AccountRecheckedSince(
                await repository.GetLastActionAtAsync(refundId, TransferFailedAction, cancellationToken)))
        {
            return Result<RefundActionResponse>.FailureResult(s_accountNotRechecked);
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

    /// <summary>
    /// Button "Nhắc khách hàng". Allowed only while the customer still has to act: the request is Pending or Failed and
    /// either has no account or the account was not re-checked since the last failure. At most one reminder per
    /// 24 hours (read from the history). Finds the buyer through the complaint's seller order or, for a Farmer-rejection
    /// refund, through the rejected seller order. Queues MSG-RF-07 in-app and by email, writes the reminder history
    /// row and saves.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Confirmation, or REFUND_ACCOUNT_NOT_NEEDED / REFUND_REMINDER_TOO_SOON / REFUND_CUSTOMER_UNKNOWN / REFUND_NOT_FOUND.</returns>
    public async Task<Result<RefundActionResponse>> RemindAccountAsync(
        long refundId,
        CancellationToken cancellationToken)
    {
        var refund = await repository.GetForProcessingAsync(refundId, cancellationToken);
        if (refund is null)
        {
            return Result<RefundActionResponse>.FailureResult(s_notFound);
        }

        var lastFailure = await repository.GetLastActionAtAsync(refundId, TransferFailedAction, cancellationToken);
        if (!refund.NeedsAccountFromCustomer(lastFailure))
        {
            return Result<RefundActionResponse>.FailureResult(s_accountNotNeeded);
        }

        var now = Now();
        var lastReminder = await repository.GetLastActionAtAsync(refundId, AccountReminderAction, cancellationToken);
        if (lastReminder is { } sent && now - sent < AccountReminderInterval)
        {
            return Result<RefundActionResponse>.FailureResult(new Error(
                "REFUND_REMINDER_TOO_SOON",
                $"Đã nhắc khách hàng lúc {sent.AddHours(7):dd/MM/yyyy HH:mm}. Mỗi yêu cầu chỉ được nhắc "
                    + "tối đa 1 lần trong 24 giờ.",
                "Conflict"));
        }

        var order = refund.DisputeTicket?.SellerOrder
            ?? await repository.GetFarmerRejectedSellerOrderAsync(refund, cancellationToken);
        if (order?.Order is null)
        {
            return Result<RefundActionResponse>.FailureResult(new Error(
                "REFUND_CUSTOMER_UNKNOWN", "Không xác định được khách hàng của yêu cầu hoàn tiền này.", "Conflict"));
        }

        long buyerId = order.Order.BuyerUserId;
        string orderCode = order.Order.OrderCode;
        // MSG-RF-07, sent again as a reminder.
        AddInApp(buyerId, refund, "Cần tài khoản nhận hoàn tiền",
            $"Khoản hoàn {refund.Amount:N0} ₫ cho đơn {orderCode} ({refund.RefundCode}) đang chờ chuyển. "
                + "Vui lòng cung cấp hoặc xác nhận tài khoản ngân hàng nhận tiền hoàn.", now);
        AddEmail(buyerId, refund, "account-reminder",
            $"Khoản hoàn tiền cho đơn {orderCode} đang chờ chuyển",
            $"AgriSustain sẽ hoàn {refund.Amount:N0} ₫. Lý do: {refund.Reason}. Vui lòng đăng nhập để cung cấp "
                + "hoặc xác nhận tài khoản ngân hàng nhận tiền hoàn.", now);
        AddAudit(refund, AccountReminderAction, refund.RefundStatus, HistoryEntry(refund), null, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            "Đã nhắc khách hàng cung cấp hoặc kiểm tra tài khoản nhận hoàn tiền qua thông báo và email."));
    }

    /// <summary>
    /// Button (23). Only a Processing request that still matches the screen can be cancelled; it returns to Pending
    /// with a CancelTransfer history row. Used when Finance opened the transfer view but sent nothing.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">ExpectedStatus from the screen.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Pending, or REFUND_NOT_PENDING / REFUND_NOT_FOUND.</returns>
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

    /// <summary>
    /// Button (20). Steps: 1) validate method, bank transaction ID and note (MSG-RF-02); 2) load the refund; it must
    /// be Processing or NeedsReconciliation; 3) the bank transaction ID must not be used by another refund (MSG-RF-03);
    /// 4) <c>refund.Complete</c> sets Completed and marks the payment Refunded when fully covered;
    /// 5) complaint source: close the complaint, set the seller order to Refunded or PartiallyRefunded, notify buyer and
    /// farmer, and reverse the refunded share of the seller order's platform fees (BR-FEE-08);
    /// Farmer-rejection source: the order stays Cancelled, both parties are only notified;
    /// 6) write the CompleteRefund history row and save everything in one SaveChanges.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">RefundMethod, TransferReference and optional Note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Completed with MSG-RF-01, or the validation / conflict error.</returns>
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
            // BR-FEE-08: reverse the refunded share of the seller order's fees by refund mode.
            if (fees is not null)
            {
                await fees.ReverseRefundAsync(sellerOrder.SellerOrderId, sellerOrder.CompletedRefundAmount,
                    sellerOrder.FinalAmount, cancellationToken);
            }
        }
        else if (await repository.GetFarmerRejectedSellerOrderAsync(refund, cancellationToken) is { } rejected)
        {
            // Farmer-rejection source: the seller order stays Cancelled; its refund state is shown
            // from this succeeded refund. Buyer and Farmer are told the money was sent (MSG186).
            NotifyRefunded(rejected, refund, now);
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

    /// <summary>
    /// Button (21). Validates the reason, requires a transfer that awaits its result (Processing or
    /// NeedsReconciliation), sets Failed, tells the buyer in-app and by email to check the receiving account, writes
    /// the RefundTransferFailed history row and saves. A later Transfer Again needs the customer to re-confirm the account.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Failure reason.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Failed, or the validation / conflict error.</returns>
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
        var failedOrder = refund.DisputeTicket?.SellerOrder
            ?? await repository.GetFarmerRejectedSellerOrderAsync(refund, cancellationToken);
        if (failedOrder is not null)
        {
            NotifyTransferFailed(failedOrder.Order.BuyerUserId, refund, reason, now);
        }

        AddAudit(refund, TransferFailedAction, previous, HistoryEntry(refund), reason, now);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<RefundActionResponse>.SuccessResult(new RefundActionResponse(
            refund.RefundTransactionId, refund.RefundStatus,
            $"Đã ghi nhận chuyển khoản {refund.RefundCode} thất bại. Khách hàng đã được thông báo "
                + "kiểm tra tài khoản nhận hoàn tiền."));
    }

    /// <summary>
    /// Button (22). Only from Processing: sets NeedsReconciliation with the note in the history. The amount stays
    /// reserved, and the request can later end only as Completed or Failed.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Reconciliation note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status NeedsReconciliation, or the validation / conflict error.</returns>
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

    /// <summary>
    /// Optimistic check against a stale screen: true when the caller sent a status and it differs from the stored one.
    /// </summary>
    /// <param name="refund">Loaded refund.</param>
    /// <param name="expectedStatus">Status the screen showed; blank skips the check.</param>
    /// <returns>True when the action must be refused with MSG-RF-04.</returns>
    private static bool IsStale(RefundTransaction refund, string? expectedStatus) =>
        !string.IsNullOrWhiteSpace(expectedStatus) && expectedStatus.Trim() != refund.RefundStatus;

    /// <summary>
    /// The BR-RF-04 history fields; the account number is always masked.
    /// </summary>
    /// <param name="refund">Refund after the change.</param>
    /// <returns>Anonymous object serialized into the history row.</returns>
    private static object HistoryEntry(RefundTransaction refund) => new
    {
        refund.RefundStatus, refund.Amount, Account = RefundTransaction.Mask(refund.ReceiverAccountNumber)
    };

    /// <summary>
    /// Refunded once completed refunds cover everything the buyer paid for this seller order;
    /// otherwise PartiallyRefunded, so the seller is still paid the remainder. The parent order
    /// status is then derived from all of its seller orders.
    /// </summary>
    /// <param name="sellerOrder">Seller order of the complaint.</param>
    /// <param name="refund">Refund just completed.</param>
    /// <param name="now">Current UTC time.</param>
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

    /// <summary>
    /// BR-RF-07: buyer in-app and by email (MSG-RF-09); the Farmer in-app only.
    /// </summary>
    /// <param name="sellerOrder">Gives the buyer, the farmer and the order code.</param>
    /// <param name="refund">Completed refund.</param>
    /// <param name="now">Current UTC time.</param>
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

    /// <summary>
    /// BR-RF-07: buyer in-app (MSG-RF-06) and by email (MSG-RF-08).
    /// </summary>
    /// <param name="buyerId">Customer to notify.</param>
    /// <param name="refund">Failed refund.</param>
    /// <param name="reason">Failure reason entered by Finance.</param>
    /// <param name="now">Current UTC time.</param>
    private void NotifyTransferFailed(long buyerId, RefundTransaction refund, string reason, DateTime now)
    {
        AddInApp(buyerId, refund, "Hoàn tiền chưa chuyển được",
            $"Khoản hoàn {refund.RefundCode} chưa chuyển được. Vui lòng kiểm tra tài khoản nhận hoàn tiền.", now);
        AddEmail(buyerId, refund, "failed",
            $"Hoàn tiền {refund.RefundCode} chưa chuyển được",
            $"Giao dịch chuyển tới tài khoản có đuôi {Last4(refund)} không thành công: {reason}. "
                + "Vui lòng đăng nhập AgriSustain và kiểm tra tài khoản nhận hoàn tiền.", now);
    }

    /// <summary>
    /// Last four digits of the receiving account for messages; "----" when there is no account.
    /// </summary>
    /// <param name="refund">Refund with the account snapshot.</param>
    /// <returns>Digits without the mask characters.</returns>
    private static string Last4(RefundTransaction refund) =>
        RefundTransaction.Mask(refund.ReceiverAccountNumber)?.TrimStart('*') ?? "----";

    /// <summary>
    /// Queues an in-app notification, stored as already Sent.
    /// </summary>
    /// <param name="userId">Recipient.</param>
    /// <param name="refund">Related refund.</param>
    /// <param name="title">Title.</param>
    /// <param name="content">Body.</param>
    /// <param name="now">Current UTC time.</param>
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

    /// <summary>
    /// Queued for the email worker, which retries only this channel (JOB-09). The PayloadJson key
    /// "refund:{id}:{event}:{ticks}" identifies the email so a retry never sends it twice.
    /// </summary>
    /// <param name="userId">Recipient.</param>
    /// <param name="refund">Related refund.</param>
    /// <param name="eventName">completed, failed or account-reminder.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="body">Email body.</param>
    /// <param name="now">Current UTC time.</param>
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

    /// <summary>
    /// Queues one append-only history row of the refund (BR-RF-04).
    /// </summary>
    /// <param name="refund">Refund acted on.</param>
    /// <param name="action">History action name.</param>
    /// <param name="oldValue">Status before the action.</param>
    /// <param name="newValue">Object serialized to JSON as the new value.</param>
    /// <param name="reason">Reason or note, when there is one.</param>
    /// <param name="now">Current UTC time.</param>
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

    /// <summary>
    /// Current UTC time from the injected TimeProvider, so tests can control the clock.
    /// </summary>
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
