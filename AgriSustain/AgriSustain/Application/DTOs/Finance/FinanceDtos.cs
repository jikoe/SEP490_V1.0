namespace AgriSustain.Application.DTOs.Finance;

public sealed record FinancePeriodQuery(string Period = "Month", DateOnly? Date = null);

public sealed record FinancePaymentQuery(
    string? Search = null, string? Category = null, string? Provider = null,
    string? Status = null, DateOnly? From = null, DateOnly? To = null,
    bool NeedsAttention = false, int Page = 1, int PageSize = 10);

public sealed record FinanceRecordsQuery(
    string Metric, string Period = "Month", DateOnly? Date = null,
    int Page = 1, int PageSize = 10);

public sealed record FinancePeriod(
    DateOnly Start, DateOnly End, DateTime From, DateTime Until, string Type);

public sealed record FinanceDashboardResponse(
    FinancePeriod Period, DateTime RefreshedAt, IReadOnlyDictionary<string, decimal?> Metrics,
    int? TransactionsToProcess, IReadOnlyList<FinancePaymentRow>? PendingTransactions,
    IReadOnlyList<string> Unavailable);

public sealed record FinanceRecord(
    long Id, string Reference, string? RelatedReference, decimal Amount,
    string Status, DateTime? At, long? PaymentId)
{
    public FinanceRecord() : this(0, "", null, 0, "", null, null) { }
}

public sealed record FinancePaymentRow(
    long Id, string Code, string? Reference, string Category, string Obligation,
    string Provider, decimal Amount, DateTime RequestedAt, DateTime? PaidAt,
    string Status, bool NeedsReview, string? Note, bool CancelledOrder)
{
    public FinancePaymentRow() : this(0, "", null, "", "", "", 0, default, null,
        "", false, null, false)
    { }
}

public sealed record FinanceObligation(
    string Category, string Reference, decimal Amount, decimal Unpaid, string Status);

public sealed record GatewayReceipt(
    string Provider, string Reference, string EventId, decimal Amount, string Status,
    DateTime? PaidAt, DateTime CheckedAt, string PayloadHash);

public sealed record ReconciliationHistory(
    long Id, string Outcome, string Message, string Before, string After,
    string Staff, DateTime At, GatewayReceipt? Receipt);

public sealed record FinancePaymentDetail(
    FinancePaymentRow Payment, string Method, DateTime? ReconciledAt, DateTime? ExpiresAt,
    FinanceObligation? Obligation, GatewayReceipt? LastReceipt,
    IReadOnlyList<ReconciliationHistory> History, string Version,
    bool CanReconcile, string? UnavailableReason);

public sealed record ReconcilePaymentRequest(string Version, string RequestKey);

public sealed record ReconcilePaymentResponse(
    string Outcome, string Message, bool AlreadyApplied, FinancePaymentDetail Detail);

public sealed record ReconciliationAudit(
    string Outcome, string Before, string After, string Version, bool RequiresReview,
    GatewayReceipt? Receipt);
