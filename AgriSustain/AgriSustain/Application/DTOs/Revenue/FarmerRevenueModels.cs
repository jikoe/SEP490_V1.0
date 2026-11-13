namespace AgriSustain.Application.DTOs.Revenue;

public sealed class RevenueFilterRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string DateBasis { get; set; } = "EventDate";
    public long? Season { get; set; }
    public string? Channel { get; set; }
    public string? PaymentStage { get; set; }
    public string? SettlementState { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record FarmerRevenueEvent(
    string Id, DateTime At, string Reference, string Kind, string Channel,
    long? SeasonId, string Season, string Stage, string State,
    decimal Sales = 0, decimal Receipts = 0, decimal Deposits = 0, decimal Refunds = 0,
    decimal Commission = 0, decimal Eligible = 0, decimal Held = 0, decimal Payout = 0,
    string? HoldReason = null, string? TransferReference = null,
    decimal? Quantity = null, decimal? UnitPrice = null, decimal? Deductions = null, decimal? EventAmount = null);

public sealed record RevenueTotals(decimal Sales, decimal Receipts, decimal Deposits, decimal Refunds,
    decimal Commission, decimal Eligible, decimal Held, decimal Payout);

public sealed record RevenueReportResponse(RevenueFilterRequest Filters, RevenueTotals Totals,
    IReadOnlyList<FarmerRevenueEvent> Items, int Total, int Page, int PageSize,
    object Chart, object Seasons, string DateBasis, string Currency, DateTime GeneratedAt, string? Message);

public sealed record RevenueExport(byte[] Content, string ContentType, string FileName);
