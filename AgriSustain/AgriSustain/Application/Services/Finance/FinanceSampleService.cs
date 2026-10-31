using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using Microsoft.Extensions.Caching.Memory;

namespace AgriSustain.Application.Services.Finance;

public sealed class FinanceSampleService(
    IFinanceRepository repository, ICurrentActor actor, IMemoryCache cache, TimeProvider clock)
    : IFinanceService
{
    private static readonly object Gate = new();

    private sealed class Ledger(DateTime now)
    {
        public Dictionary<long, FinancePaymentDetail> Payments { get; } = CreatePayments(now);
        public Dictionary<string, (string Version, ReconcilePaymentResponse Response)> Requests { get; } = new();
    }

    private Ledger Data => cache.GetOrCreate($"finance-sample:{actor.UserId}", entry =>
    {
        entry.SlidingExpiration = TimeSpan.FromHours(2);
        return new Ledger(clock.GetUtcNow().UtcDateTime);
    })!;

    public async Task<Result<FinanceDashboardResponse>> DashboardAsync(
        FinancePeriodQuery query, CancellationToken ct)
    {
        if (!await CanAccess(ct))
        {
            return Denied<FinanceDashboardResponse>();
        }
        var period = FinanceQueryValidator.Period(query, clock.GetUtcNow().UtcDateTime);
        if (period == null)
        {
            return Invalid<FinanceDashboardResponse>();
        }
        lock (Gate)
        {
            var metrics = FinanceQueryValidator.Metrics.ToDictionary(metric => metric,
                metric => (decimal?)SourceRows(Data, metric, period).Sum(row => row.Amount));
            var pending = Data.Payments.Values.Select(detail => detail.Payment)
                .Where(row => row.RequestedAt >= period.From && row.RequestedAt < period.Until)
                .Where(row => row.Status == "Pending" || row.NeedsReview)
                .OrderByDescending(row => row.RequestedAt).ToArray();
            return Result<FinanceDashboardResponse>.SuccessResult(new(period,
                clock.GetUtcNow().UtcDateTime, metrics, pending.Length, pending.Take(3).ToArray(), []));
        }
    }

    public async Task<Result<PagedResponse<FinanceRecord>>> RecordsAsync(
        FinanceRecordsQuery query, CancellationToken ct)
    {
        if (!await CanAccess(ct))
        {
            return Denied<PagedResponse<FinanceRecord>>();
        }
        var period = FinanceQueryValidator.Period(new(query.Period, query.Date), clock.GetUtcNow().UtcDateTime);
        if (period == null || !FinanceQueryValidator.Metrics.Contains(query.Metric)
            || !FinanceQueryValidator.IsPageValid(query.Page, query.PageSize))
        {
            return Invalid<PagedResponse<FinanceRecord>>();
        }
        lock (Gate)
        {
            var rows = SourceRows(Data, query.Metric, period).OrderByDescending(row => row.At).ToArray();
            return Result<PagedResponse<FinanceRecord>>.SuccessResult(new(
                rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(),
                rows.Length, query.Page, query.PageSize));
        }
    }

    public async Task<Result<PagedResponse<FinancePaymentRow>>> SearchAsync(
        FinancePaymentQuery query, CancellationToken ct)
    {
        if (!await CanAccess(ct))
        {
            return Denied<PagedResponse<FinancePaymentRow>>();
        }
        if (!FinanceQueryValidator.IsValid(query))
        {
            return Invalid<PagedResponse<FinancePaymentRow>>();
        }
        lock (Gate)
        {
            var rows = Data.Payments.Values.Select(detail => detail.Payment).Where(row =>
                (string.IsNullOrEmpty(query.Category) || row.Category == query.Category)
                && (string.IsNullOrEmpty(query.Provider) || row.Provider == query.Provider)
                && (string.IsNullOrEmpty(query.Status) || row.Status == query.Status)
                && (!query.NeedsAttention || row.Status == "Pending" || row.NeedsReview)
                && (query.From == null || row.RequestedAt >= FinanceQueryValidator.Utc(query.From.Value))
                && (query.To == null || row.RequestedAt < FinanceQueryValidator.Utc(query.To.Value.AddDays(1)))
                && (string.IsNullOrWhiteSpace(query.Search)
                    || (row.Code + " " + row.Reference + " " + row.Obligation)
                        .Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(row => row.RequestedAt).ToArray();
            return Result<PagedResponse<FinancePaymentRow>>.SuccessResult(new(
                rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(),
                rows.Length, query.Page, query.PageSize));
        }
    }

    public async Task<Result<FinancePaymentDetail>> DetailAsync(long id, CancellationToken ct)
    {
        if (!await CanAccess(ct))
        {
            return Denied<FinancePaymentDetail>();
        }
        lock (Gate)
        {
            return Data.Payments.TryGetValue(id, out var detail)
                ? Result<FinancePaymentDetail>.SuccessResult(detail) : Missing<FinancePaymentDetail>();
        }
    }

    public async Task<Result<ReconcilePaymentResponse>> ReconcileAsync(
        long id, ReconcilePaymentRequest request, CancellationToken ct)
    {
        if (!await CanAccess(ct))
        {
            return Denied<ReconcilePaymentResponse>();
        }
        if (!FinanceQueryValidator.IsValid(request))
        {
            return Invalid<ReconcilePaymentResponse>();
        }
        lock (Gate)
        {
            var ledger = Data;
            if (!ledger.Payments.TryGetValue(id, out var detail))
            {
                return Missing<ReconcilePaymentResponse>();
            }
            string key = $"{id}:{request.RequestKey}";
            if (ledger.Requests.TryGetValue(key, out var previous))
            {
                if (previous.Version != request.Version)
                {
                    return Invalid<ReconcilePaymentResponse>("Yêu cầu đã được dùng cho lần đối soát khác.");
                }
                return Result<ReconcilePaymentResponse>.SuccessResult(previous.Response with
                {
                    AlreadyApplied = true,
                    Detail = detail
                });
            }
            if (detail.Version != request.Version)
            {
                return Invalid<ReconcilePaymentResponse>(
                "Dữ liệu mẫu đã thay đổi. Vui lòng tải lại.");
            }
            if (detail.Payment.Status == "Succeeded")
            {
                return Result<ReconcilePaymentResponse>.SuccessResult(new("AlreadyApplied",
                    "Khoản thanh toán mẫu đã được ghi nhận; không cộng tiền lần nữa.", true, detail));
            }
            var receipt = detail.LastReceipt! with { CheckedAt = clock.GetUtcNow().UtcDateTime };
            string outcome = receipt.Status == "Succeeded" && receipt.Amount != detail.Payment.Amount
                ? "Mismatch" : detail.Payment.CancelledOrder && receipt.Status == "Succeeded"
                    ? "LatePayment" : receipt.Status;
            string message = outcome switch
            {
                "Succeeded" => "Đối soát mẫu thành công. Không thay đổi dữ liệu tài chính thật.",
                "Mismatch" => "Số tiền từ cổng không khớp số tiền của đơn hàng. Giữ nguyên trạng thái để kiểm tra.",
                "Failed" => "Cổng thanh toán mẫu ghi nhận giao dịch thất bại.",
                "Expired" => "Giao dịch mẫu đã hết hạn thanh toán.",
                "LatePayment" => "Đã ghi nhận tiền mẫu nhưng đơn đã hủy. Cần kiểm tra trước khi xử lý tiếp.",
                _ => "Giao dịch mẫu vẫn đang chờ xác minh thanh toán."
            };
            string status = outcome is "Succeeded" or "Failed" or "Expired" ? outcome : detail.Payment.Status;
            var history = new ReconciliationHistory(detail.History.Count + 1, outcome, message,
                detail.Payment.Status, status, $"Nhân viên tài chính #{actor.UserId}", receipt.CheckedAt, receipt);
            var updated = detail with
            {
                Payment = detail.Payment with
                {
                    Status = status,
                    Note = message,
                    NeedsReview = outcome is "Mismatch" or "LatePayment",
                    PaidAt = outcome == "Succeeded" ? receipt.PaidAt : detail.Payment.PaidAt
                },
                Obligation = outcome == "Succeeded" ? detail.Obligation! with
                {
                    Unpaid = 0,
                    Status = detail.Payment.Category == "Subscription" ? "Active" : "Paid"
                } : detail.Obligation,
                LastReceipt = receipt,
                ReconciledAt = receipt.CheckedAt,
                History = new[] { history }.Concat(detail.History).ToArray(),
                Version = Hash(detail.Version + request.RequestKey),
                CanReconcile = status != "Succeeded"
            };
            ledger.Payments[id] = updated;
            var response = new ReconcilePaymentResponse(outcome, message, false, updated);
            ledger.Requests[key] = (request.Version, response);
            return Result<ReconcilePaymentResponse>.SuccessResult(response);
        }
    }

    private static Dictionary<long, FinancePaymentDetail> CreatePayments(DateTime now)
    {
        var local = now.AddHours(7);
        var created = new DateTime(local.Year, local.Month, Math.Min(23, local.Day), 1, 0, 0,
            DateTimeKind.Utc);
        var amounts = new[] { 855000m, 455000m, 499000m, 385000m, 999000m, 555000m, 1650000m, 499000m };
        var result = new Dictionary<long, FinancePaymentDetail>();
        for (int index = 0; index < amounts.Length; index++)
        {
            long id = index + 1;
            bool subscription = index is 2 or 4 or 7;
            string code = subscription ? $"PAY-SUB-{local.Year}-00{(index == 2 ? 3 : index == 4 ? 2 : 1)}"
                : $"PAY-{local.Year}-010{(index == 0 ? 8 : index == 1 ? 7 : index == 3 ? 6 : index == 5 ? 5 : 4)}";
            string provider = index is 1 or 3 or 6 ? "VietQR" : "PayOS";
            string reference = $"{(provider == "PayOS" ? "POS" : "VQR")}-{local:yyMM}-{id:0000}";
            string obligation = subscription ? $"SUB #{203 - index} · Standard" : $"ORD-{local.Year}-010{8 - index}";
            string status = index is 3 or 7 ? "Succeeded" : "Pending";
            string gatewayStatus = index == 4 ? "Expired" : index == 5 ? "Failed" : "Succeeded";
            DateTime at = created.AddDays(-index);
            var receipt = new GatewayReceipt(provider, reference, $"sample-{id}",
                gatewayStatus != "Succeeded" ? 0 : index == 1 ? 450000 : amounts[index], gatewayStatus,
                gatewayStatus == "Succeeded" ? at.AddHours(1) : null, now, Hash($"sample-{id}"));
            var row = new FinancePaymentRow(id, code, reference, subscription ? "Subscription" : "Order",
                obligation, provider, amounts[index], at, status == "Succeeded" ? at.AddHours(1) : null,
                status, index == 1, index == 1 ? "Có sai lệch số tiền" : null, index == 6);
            result[id] = new(row, provider == "VietQR" ? "BankTransfer" : "OnlineBanking", null,
                at.AddHours(2), new(row.Category, obligation, amounts[index],
                    status == "Succeeded" ? 0 : amounts[index], index == 6 ? "Cancelled"
                        : status == "Succeeded" ? subscription ? "Active" : "Paid" : "PendingPayment"),
                receipt, [], Hash($"sample-initial-{id}-{now:O}"), status != "Succeeded", null);
        }
        return result;
    }

    private static IEnumerable<FinanceRecord> SourceRows(Ledger data, string metric, FinancePeriod period)
    {
        var payments = data.Payments.Values.Select(detail => detail.Payment).ToArray();
        var at = payments[0].RequestedAt;
        IEnumerable<FinanceRecord> records = metric switch
        {
            "Subscriptions" => payments.Where(row => row.Category == "Subscription" && row.Status == "Succeeded")
                .Select(row => new FinanceRecord(row.Id, row.Code, row.Obligation,
                    row.Amount, row.Status, row.PaidAt, row.Id)),
            "Gmv" => payments.Where(row => row.Category == "Order" && !row.CancelledOrder)
                .Select(row => new FinanceRecord(row.Id, row.Obligation, null,
                    row.Amount, "Confirmed", row.RequestedAt, null)),
            "Commission" => [new(101, "PAYOUT-001", "Hoa hồng đã quyết toán", 82000, "Paid", at, null)],
            "Refunds" => [new(102, "REFUND-001", "ORD-2026-0099", 100000, "Succeeded", at, null)],
            "PendingPayouts" => [new(103, "PAYOUT-002", "Nông trại đang chờ chi trả", 1925000, "Pending", at, null)],
            _ => [new(104, "PAYOUT-001", "Đợt chi trả hoàn tất", 918000, "Paid", at, null)]
        };
        return records.Where(row => row.At >= period.From && row.At < period.Until);
    }

    private Task<bool> CanAccess(CancellationToken ct) => repository.CanAccessAsync(actor.UserId, ct);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Result<T> Denied<T>() => Result<T>.FailureResult(new("FINANCE_ACCESS_DENIED",
        "Vui lòng đăng nhập bằng tài khoản nhân viên tài chính đang hoạt động.", "Forbidden"));
    private static Result<T> Missing<T>() => Result<T>.FailureResult(new("PAYMENT_NOT_FOUND",
        "Không tìm thấy giao dịch mẫu.", "NotFound"));
    private static Result<T> Invalid<T>(string message = "Kỳ báo cáo hoặc bộ lọc không hợp lệ.") =>
        Result<T>.FailureResult(new("INVALID_INPUT", message));
}
