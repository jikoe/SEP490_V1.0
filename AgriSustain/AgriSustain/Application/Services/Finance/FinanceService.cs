using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Finance;

public sealed class FinanceService(
    IFinanceRepository repository, ICurrentActor actor,
    TimeProvider clock, ILogger<FinanceService> logger, IFinancePaymentReconciliation? reconciliation = null)
    : IFinanceService
{
    private static readonly Error Denied = new("FINANCE_ACCESS_DENIED",
        "Vui lòng đăng nhập bằng tài khoản nhân viên tài chính đang hoạt động.", "Forbidden");
    private static readonly Error Missing = new("PAYMENT_NOT_FOUND",
        "Không tìm thấy giao dịch thanh toán.", "NotFound");
    public async Task<Result<FinanceDashboardResponse>> DashboardAsync(
        FinancePeriodQuery query, CancellationToken ct)
    {
        if (!await repository.CanAccessAsync(actor.UserId, ct))
        {
            return Fail<FinanceDashboardResponse>(Denied);
        }
        var period = FinanceQueryValidator.Period(query, Now());
        if (period == null)
        {
            return Invalid<FinanceDashboardResponse>("Vui lòng chọn kỳ báo cáo hợp lệ.");
        }
        var metrics = new Dictionary<string, decimal?>();
        var unavailable = new List<string>();
        foreach (string metric in FinanceQueryValidator.Metrics)
        {
            try
            {
                metrics[metric] = await repository.MetricAsync(metric, period, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Finance metric unavailable: {Metric}", metric);
                metrics[metric] = null;
                unavailable.Add(metric);
            }
        }
        PagedResponse<FinancePaymentRow>? pending = null;
        try
        {
            pending = await repository.SearchAsync(new FinancePaymentQuery(
                From: period.Start, To: period.End, NeedsAttention: true, PageSize: 3), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Finance pending payments unavailable");
            unavailable.Add("Transactions");
        }
        return Result<FinanceDashboardResponse>.SuccessResult(new(period, Now(), metrics,
            pending?.Total, pending?.Items, unavailable));
    }

    public async Task<Result<PagedResponse<FinanceRecord>>> RecordsAsync(
        FinanceRecordsQuery query, CancellationToken ct)
    {
        if (!await repository.CanAccessAsync(actor.UserId, ct))
        {
            return Fail<PagedResponse<FinanceRecord>>(Denied);
        }
        var period = FinanceQueryValidator.Period(new(query.Period, query.Date), Now());
        if (period == null || !FinanceQueryValidator.Metrics.Contains(query.Metric)
            || !FinanceQueryValidator.IsPageValid(query.Page, query.PageSize))
        {
            return Invalid<PagedResponse<FinanceRecord>>("Kỳ báo cáo hoặc bộ lọc không hợp lệ.");
        }
        return Result<PagedResponse<FinanceRecord>>.SuccessResult(await repository.RecordsAsync(
            query.Metric, period, query.Page, query.PageSize, ct));
    }

    public async Task<Result<PagedResponse<FinancePaymentRow>>> SearchAsync(
        FinancePaymentQuery query, CancellationToken ct)
    {
        if (!await repository.CanAccessAsync(actor.UserId, ct))
        {
            return Fail<PagedResponse<FinancePaymentRow>>(Denied);
        }
        if (!FinanceQueryValidator.IsValid(query))
        {
            return Invalid<PagedResponse<FinancePaymentRow>>(
                "Bộ lọc không hợp lệ. Ngày bắt đầu phải không sau ngày kết thúc.");
        }
        return Result<PagedResponse<FinancePaymentRow>>.SuccessResult(
            await repository.SearchAsync(query, ct));
    }

    public async Task<Result<FinancePaymentDetail>> DetailAsync(long id, CancellationToken ct)
    {
        if (!await repository.CanAccessAsync(actor.UserId, ct))
        {
            return Fail<FinancePaymentDetail>(Denied);
        }
        var payment = await repository.PaymentAsync(id, ct);
        if (payment == null || !HasObligation(payment))
        {
            return Fail<FinancePaymentDetail>(Missing);
        }
        return Result<FinancePaymentDetail>.SuccessResult(BuildDetail(payment,
            await repository.InvoicesAsync(payment, ct), await repository.HistoryAsync(id, ct)));
    }

    public async Task<Result<ReconcilePaymentResponse>> ReconcileAsync(
        long id, ReconcilePaymentRequest request, CancellationToken ct)
    {
        if (!await repository.CanAccessAsync(actor.UserId, ct))
        {
            return Fail<ReconcilePaymentResponse>(Denied);
        }
        if (!FinanceQueryValidator.IsValid(request))
        {
            return Invalid<ReconcilePaymentResponse>("Yêu cầu không hợp lệ. Vui lòng tải lại giao dịch.");
        }
        var payment = await repository.PaymentAsync(id, ct);
        if (payment == null || !HasObligation(payment))
        {
            return Fail<ReconcilePaymentResponse>(Missing);
        }
        if (UnavailableReason(payment) is { } reason)
        {
            return Invalid<ReconcilePaymentResponse>(reason);
        }
        var result = await reconciliation!.CheckAsync(payment, actor.UserId, request, ct);
        if (!result.Success)
        {
            return Fail<ReconcilePaymentResponse>(result.Error!);
        }
        var detail = await DetailAsync(id, ct);
        return detail.Success
            ? Result<ReconcilePaymentResponse>.SuccessResult(new(result.Value!.Outcome,
                result.Value.Message, result.Value.AlreadyApplied, detail.Value!))
            : Fail<ReconcilePaymentResponse>(detail.Error!);
    }

    private static bool HasObligation(PaymentTransaction payment) =>
        payment.OrderId != null || payment.B2bsubscriptionId != null
        || FinancePaymentRules.SubscriptionReference(payment) != null;

    private FinancePaymentDetail BuildDetail(
        PaymentTransaction payment, IReadOnlyList<Invoice> invoices, IReadOnlyList<AuditLog> audits)
    {
        var history = audits.Select(audit =>
        {
            var data = ReadAudit(audit);
            return new ReconciliationHistory(audit.AuditLogId, data?.Outcome ?? "Recorded",
                audit.Reason ?? "Đã ghi nhận kết quả kiểm tra.", data?.Before ?? audit.OldValue ?? "",
                data?.After ?? payment.PaymentStatus,
                audit.PerformedByUser?.FullName ?? "Hệ thống", audit.OccurredAt, data?.Receipt);
        }).ToArray();
        string? unavailable = UnavailableReason(payment);
        var obligation = payment.Order is { } order
            ? new FinanceObligation("Order", order.OrderCode, order.FinalAmount,
                FinancePaymentRules.Unpaid(payment), order.OrderStatus)
            : payment.B2bsubscription is { } subscription
                ? new FinanceObligation("Subscription", $"SUB #{subscription.B2bsubscriptionId} · "
                    + subscription.Package.PackageName, subscription.PriceSnapshot,
                    FinancePaymentRules.Unpaid(payment), subscription.SubscriptionStatus)
                : FinancePaymentRules.SubscriptionReference(payment) is { } reference
                    ? new FinanceObligation("Subscription", reference, payment.Amount,
                        FinancePaymentRules.Unpaid(payment), payment.PaymentStatus)
                    : null;
        var latest = history.FirstOrDefault();
        var evidence = payment.PaymentGatewayEvents.OrderByDescending(entry => entry.ReceivedAt)
            .ThenByDescending(entry => entry.PaymentGatewayEventId).FirstOrDefault();
        var receipt = history.FirstOrDefault(entry => entry.Receipt != null)?.Receipt
            ?? (evidence == null ? null : new GatewayReceipt(evidence.GatewayProvider,
                evidence.ProviderTransactionReference, evidence.ProviderEventId, evidence.Amount,
                evidence.EventStatus, payment.PaidAt, evidence.ReceivedAt, evidence.PayloadHash));
        var expectedReference = payment.GatewayProvider == "SePay" && FinancePaymentRules.IsSupported(payment)
            ? FinancePaymentRules.TransferReference(payment) : payment.ProviderTransactionReference;
        if (receipt == null && payment.GatewayProvider == "SePay" && expectedReference is { Length: > 0 }
            && FinancePaymentRules.IsCaptured(payment) && payment.WebhookPayload is { Length: > 0 } payload
            && payment.TransactionCode.StartsWith("SEPAY-", StringComparison.Ordinal)
            && long.TryParse(payment.TransactionCode.AsSpan(6), out long sePayId) && sePayId > 0)
        {
            receipt = new("SePay", expectedReference, payment.TransactionCode, payment.Amount, "Succeeded",
                payment.PaidAt, payment.PaidAt!.Value,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), payment.ProviderTransactionReference);
        }
        var row = new FinancePaymentRow(payment.PaymentTransactionId, payment.TransactionCode, expectedReference,
            obligation?.Category ?? "Other",
            obligation?.Reference ?? "Chưa có nghĩa vụ liên kết", payment.GatewayProvider,
            payment.Amount, payment.RequestedAt, payment.PaidAt, payment.PaymentStatus,
            latest?.Outcome is "Mismatch" or "LatePayment" or "VerificationFailed" or "NeedsReview",
            latest?.Message, payment.Order?.OrderStatus == "Cancelled");
        return new(row, payment.PaymentType, payment.ReconciledAt, payment.ExpiresAt,
            obligation, receipt, history,
            FinancePaymentRules.Version(payment, invoices),
            unavailable == null && !FinancePaymentRules.IsCaptured(payment), unavailable);
    }

    private string? UnavailableReason(PaymentTransaction payment) =>
        !FinancePaymentRules.IsSupported(payment) ? "Giao dịch này không có yêu cầu thanh toán phù hợp."
        : payment.GatewayProvider != "SePay" ? "Đối soát trực tiếp chỉ khả dụng với giao dịch SePay/VietQR."
        : string.IsNullOrEmpty(FinancePaymentRules.TransferReference(payment)) ? "Giao dịch chưa có mã chuyển khoản."
        : reconciliation?.IsConfigured != true ? "Chưa cấu hình đối soát SePay cho tài khoản nhận tiền."
        : null;

    private static ReconciliationAudit? ReadAudit(AuditLog audit)
    {
        try
        {
            return JsonSerializer.Deserialize<ReconciliationAudit>(audit.NewValue ?? "null");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
    private static Result<T> Fail<T>(Error error) => Result<T>.FailureResult(error);
    private static Result<T> Invalid<T>(string message) => Fail<T>(new("INVALID_INPUT", message));
}
