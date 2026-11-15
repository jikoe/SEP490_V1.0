using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Refunds;

public sealed class FinanceRejectedOrderRefundService(
    IFinanceRejectedOrderRefundRepository repository,
    IRejectedOrderRefundGateway gateway,
    RejectedOrderRefundRules rules,
    RejectedOrderRefundNotificationService notifications,
    IOrderFeeService fees,
    TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static FeatureResult<T> Fail<T>(string message, string code = "CONFLICT") => FeatureResult<T>.Fail(code, message);

    public async Task<FeatureResult<object>> SearchAsync(long actor, RejectedOrderRefundQuery query, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<object>(RejectedOrderRefundMessages.Forbidden, "FORBIDDEN");
        if (query.Page < 1 || query.Page > 100000 || query.Keyword?.Length > 255
            || query.From > query.To || query.To == DateOnly.MaxValue
            || query.Status is not (null or "" or "Pending" or "Processing" or "NeedsReconciliation" or "Succeeded"))
            return Fail<object>(RejectedOrderRefundMessages.Invalid, "VALIDATION");
        return FeatureResult<object>.Ok(await repository.SearchAsync(query, ct));
    }

    public async Task<FeatureResult<object>> DetailAsync(long actor, long id, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<object>(RejectedOrderRefundMessages.Forbidden, "FORBIDDEN");
        var context = await repository.LoadAsync(id, false, ct);
        if (context == null) return Fail<object>(RejectedOrderRefundMessages.Missing, "NOT_FOUND");
        var r = context.Refund;
        var o = context.Order;
        var attempt = Attempt(context);
        return FeatureResult<object>.Ok(new
        {
            id, code = r.RefundCode, status = RejectedOrderRefundRules.Succeeded(r) ? "Succeeded" : r.RefundStatus,
            orderCode = o.SellerOrderCode, orderStatus = o.Status, customer = o.Order.BuyerUser.FullName,
            farmer = o.FarmerProfile.FarmName, reason = r.Reason, o.Subtotal, o.DiscountShare, o.DeliveryFee,
            o.FinalAmount, amount = r.Amount, paymentCode = r.PaymentTransaction.TransactionCode,
            verifiedPayment = r.PaymentTransaction.Amount,
            otherReserved = r.PaymentTransaction.RefundTransactions.Where(x => x.RefundTransactionId != id && x.ReservesPayment).Sum(x => x.Amount),
            recipient = new { bankCode = r.ReceiverBankCode, accountNumber = r.ReceiverAccountNumber,
                accountHolder = r.ReceiverAccountHolder },
            canOpenQr = rules.Eligible(context, Now), expectedVersion = rules.Version(context),
            eligibilityMessage = r.RefundStatus == "Pending" && !rules.Eligible(context, Now)
                ? RejectedOrderRefundMessages.Ineligible : null,
            plan = attempt?.Plan, r.GatewayReference, r.ProcessedAt,
            submittedAt = o.OrderStatusHistories.Where(x => x.NewStatus == "Cancelled")
                .Select(x => (DateTime?)x.OccurredAt).Min(),
            history = o.OrderStatusHistories.Where(x => x.NewStatus == "Cancelled")
                .Select(x => new { Action = "Requested", x.OccurredAt, x.Reason })
                .Concat(context.History.Select(x => new { x.Action, x.OccurredAt, x.Reason }))
                .OrderBy(x => x.OccurredAt)
        });
    }

    public async Task<FeatureResult<RejectedOrderRefundPreview>> PreviewAsync(long actor, long id, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct))
            return Fail<RejectedOrderRefundPreview>(RejectedOrderRefundMessages.Forbidden, "FORBIDDEN");
        var context = await repository.LoadAsync(id, false, ct);
        if (context == null) return Fail<RejectedOrderRefundPreview>(RejectedOrderRefundMessages.Missing, "NOT_FOUND");
        if (!rules.Eligible(context, Now)) return Fail<RejectedOrderRefundPreview>(RejectedOrderRefundMessages.Ineligible);
        var plan = gateway.CreateQr(context.Refund);
        if (plan == null || !await gateway.CheckReadAccessAsync(plan, ct))
            return Fail<RejectedOrderRefundPreview>(RejectedOrderRefundMessages.Load, "UNAVAILABLE");
        return FeatureResult<RejectedOrderRefundPreview>.Ok(new(rules.Version(context), plan,
            context.Refund.ReceiverAccountHolder!, "Xác nhận đúng số tiền, mã yêu cầu và tài khoản nhận đã lưu trước khi mở QR."));
    }

    public async Task<FeatureResult<RejectedOrderRefundResult>> OpenQrAsync(long actor, long id,
        OpenRejectedOrderRefundRequest request, CancellationToken ct)
    {
        if (request == null || !Guid.TryParseExact(request.RequestKey, "D", out _) || request.ExpectedVersion?.Length != 64)
            return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Invalid, "VALIDATION");
        var preview = await PreviewAsync(actor, id, ct);
        if (!preview.IsSuccess) return FeatureResult<RejectedOrderRefundResult>.Fail(preview.Error!.Code, preview.Error.Message);
        return await repository.AtomicAsync(async () =>
        {
            if (!await repository.AuthorizedAsync(actor, ct)) return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Forbidden, "FORBIDDEN");
            var context = await repository.LoadAsync(id, true, ct);
            if (context == null) return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Missing, "NOT_FOUND");
            if (rules.Version(context) != request.ExpectedVersion || !rules.Eligible(context, Now))
                return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Stale);
            var plan = gateway.CreateQr(context.Refund);
            if (plan == null || plan.SourceAccountNumber != preview.Value!.Plan.SourceAccountNumber
                || plan.SourceBankCode != preview.Value.Plan.SourceBankCode)
                return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Ineligible);
            context.Refund.StartTransfer();
            context.Refund.ProcessedByUserId = actor;
            var attempt = new RejectedOrderRefundAttempt(plan with { OpenedAt = Now },
                context.Refund.ReceiverAccountHolder!, actor, request.RequestKey);
            Audit(context, actor, "QrOpened", request.RequestKey, JsonSerializer.Serialize(attempt), RejectedOrderRefundMessages.Opened);
            return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "Processing", RejectedOrderRefundMessages.Opened));
        }, ct);
    }

    public async Task<FeatureResult<RejectedOrderRefundResult>> ReconcileAsync(long actor, long id, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Forbidden, "FORBIDDEN");
        return await ReconcileObservedAsync(id, ct);
    }

    // Used by the background worker; it can only settle an already authorized QR attempt.
    internal async Task<FeatureResult<RejectedOrderRefundResult>> ReconcileObservedAsync(long id, CancellationToken ct)
    {
        var before = await repository.LoadAsync(id, false, ct);
        if (before == null) return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Missing, "NOT_FOUND");
        if (RejectedOrderRefundRules.Succeeded(before.Refund))
            return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "Succeeded", Completion(before)));
        var attempt = Attempt(before);
        if (attempt == null || before.Refund.RefundStatus is not ("Processing" or "NeedsReconciliation"))
            return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Stale);
        var lookup = await gateway.GetSourceDebitsAsync(attempt.Plan, ct);
        if (!lookup.LookupAvailable)
            return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, before.Refund.RefundStatus, RejectedOrderRefundMessages.Pending));
        return await repository.AtomicAsync(async () =>
        {
            var context = await repository.LoadAsync(id, true, ct);
            if (context == null) return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Missing, "NOT_FOUND");
            if (RejectedOrderRefundRules.Succeeded(context.Refund))
                return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "Succeeded", Completion(context)));
            var current = Attempt(context);
            if (current != attempt || context.Refund.RefundStatus is not ("Processing" or "NeedsReconciliation"))
                return Fail<RejectedOrderRefundResult>(RejectedOrderRefundMessages.Stale);
            var plan = current.Plan;
            var r = context.Refund;
            bool unchanged = r.Amount == plan.Amount && r.RefundCode == plan.OperationKey
                && r.ReceiverBankCode == plan.RecipientBankCode && r.ReceiverAccountNumber == plan.RecipientAccountNumber
                && r.ReceiverAccountHolder == current.RecipientName && context.Order.Status == "Cancelled";
            var candidates = lookup.Transfers.Where(x => HasReference(x.Content, plan.OperationKey)).ToList();
            var matches = candidates.Where(x => BankKey(x.Gateway) == BankKey(plan.SourceBankCode)
                && FarmerPayoutRules.Digits(x.SourceAccountNumber) == FarmerPayoutRules.Digits(plan.SourceAccountNumber)
                && x.Amount == plan.Amount && x.At >= plan.OpenedAt.AddSeconds(-5) && x.At <= Now.AddMinutes(2)
                && !string.IsNullOrWhiteSpace(x.BankReference)).ToList();
            if (!unchanged || candidates.Count > 1 || candidates.Count > 0 && matches.Count != 1)
            {
                MarkConflict(context, current.FinanceUserId);
                return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "NeedsReconciliation", RejectedOrderRefundMessages.Pending));
            }
            if (matches.Count == 0)
                return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, r.RefundStatus, RejectedOrderRefundMessages.Pending));
            var transfer = matches[0];
            if (await repository.BankEventUsedAsync(FarmerPayoutRules.Hash("sepay-out:" + transfer.EventId), transfer.BankReference!, ct))
            {
                MarkConflict(context, current.FinanceUserId);
                return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "NeedsReconciliation", RejectedOrderRefundMessages.Pending));
            }
            if (r.PaymentTransaction.RefundTransactions.Where(x => x.ReservesPayment).Sum(x => x.Amount) > r.PaymentTransaction.Amount)
            {
                MarkConflict(context, current.FinanceUserId);
                return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "NeedsReconciliation", RejectedOrderRefundMessages.Pending));
            }
            r.Complete(current.FinanceUserId, "BankTransfer", transfer.BankReference!, transfer.At);
            // Cancellation already reversed fees. This operation is idempotent for any remaining fee lines.
            await fees.ReverseCancelledAsync(context.Order.SellerOrderId, ct);
            Audit(context, current.FinanceUserId, "SePayVerified", FarmerPayoutRules.Hash("sepay-out:" + transfer.EventId),
                JsonSerializer.Serialize(new { transfer.EventId, transfer.BankReference, transfer.At, transfer.Amount, transfer.Gateway, transfer.SourceAccountNumber, transfer.Content }), Completion(context));
            notifications.QueueSucceeded(context, Now);
            return FeatureResult<RejectedOrderRefundResult>.Ok(new(id, "Succeeded", Completion(context)));
        }, ct);
    }

    private void MarkConflict(RejectedOrderRefundContext context, long actor)
    {
        if (context.Refund.RefundStatus == "NeedsReconciliation") return;
        context.Refund.RefundStatus = "NeedsReconciliation";
        Audit(context, actor, "NeedsReconciliation", null, null, RejectedOrderRefundMessages.Pending);
    }

    private void Audit(RejectedOrderRefundContext context, long actor, string action, string? key, string? value, string reason) =>
        repository.Audit(new AuditLog
        {
            EntityType = "RejectedOrderRefund", EntityId = context.Refund.RefundTransactionId,
            PerformedByUserId = actor, Action = action, TraceId = key, NewValue = value,
            Reason = reason, OccurredAt = Now
        });

    private static string Completion(RejectedOrderRefundContext context) => RejectedOrderRefundMessages.Succeeded(
        context.Refund.RefundCode, context.Order.SellerOrderCode, context.Refund.Amount,
        context.Refund.ReceiverAccountNumber, context.Refund.GatewayReference!);

    private static RejectedOrderRefundAttempt? Attempt(RejectedOrderRefundContext context)
    {
        string? json = context.History.LastOrDefault(x => x.Action == "QrOpened")?.NewValue;
        try { return json == null ? null : JsonSerializer.Deserialize<RejectedOrderRefundAttempt>(json); }
        catch (JsonException) { return null; }
    }

    private static string BankKey(string bank) => FarmerPayoutRules.BankKey(bank) is "MB" or "MBBANK" or "MILITARYBANK"
        ? "MB" : FarmerPayoutRules.BankKey(bank);

    private static bool HasReference(string content, string reference) => System.Text.RegularExpressions.Regex.IsMatch(
        content, @"(?<![A-Z0-9])" + System.Text.RegularExpressions.Regex.Escape(reference) + @"(?![A-Z0-9])",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
