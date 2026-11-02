namespace AgriSustain.Application.DTOs.FarmManagement;

public sealed record FarmQuery
{
    public string? Keyword { get; init; }

    public string? Status { get; init; }

    public string? Activity { get; init; }

    public long? ProductId { get; init; }

    public long? SeasonId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

public record FarmCommand
{
    public string RequestKey { get; init; } = "";

    public string? Version { get; init; }
}

public sealed record SeasonPlanRequest : FarmCommand
{
    public string SeasonName { get; init; } = "";

    public long ProductId { get; init; }

    public string CropVariety { get; init; } = "";

    public decimal CultivatedArea { get; init; }

    public DateOnly PlantingDate { get; init; }

    public DateOnly EstimatedHarvestDate { get; init; }

    public decimal EstimatedYield { get; init; }

    public string YieldUnit { get; init; } = "";
}

public sealed record SeasonActionRequest : FarmCommand
{
    public string NextStage { get; init; } = "";

    public DateOnly? CompletionDate { get; init; }

    public decimal ActualYield { get; init; }

    public string Outcome { get; init; } = "";

    public string Reason { get; init; } = "";

    public string[] EvidenceUrls { get; init; } = [];
}

public sealed record FarmingLogRequest : FarmCommand
{
    public long CropSeasonId { get; init; }

    public DateOnly ActionDate { get; init; }

    public string ActionType { get; init; } = "";

    public string DetailDescription { get; init; } = "";

    public string PerformedBy { get; init; } = "";

    public string? InputMaterial { get; init; }

    public string? Dosage { get; init; }

    public string CorrectionReason { get; init; } = "";

    public string[] EvidenceUrls { get; init; } = [];
}

public sealed record HarvestBatchRequest : FarmCommand
{
    public long ProductId { get; init; }

    public long CropSeasonId { get; init; }

    public string BatchCode { get; init; } = "";

    public DateOnly? EstimatedHarvestDate { get; init; }

    public decimal EstimatedYield { get; init; }

    public DateOnly? ActualHarvestDate { get; init; }

    public decimal ActualYield { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public string Unit { get; init; } = "";

    public string? QualityNotes { get; init; }

    public string Reason { get; init; } = "";
}

public sealed record BatchIssueRequest : FarmCommand
{
    public string IssueType { get; init; } = "";

    public decimal Quantity { get; init; }

    public string Reason { get; init; } = "";

    public string[] EvidenceUrls { get; init; } = [];
}

public sealed record FarmProductOption(long ProductId, string ProductName, string Unit);
public sealed record FarmSeasonOption(
    long CropSeasonId,
    long ProductId,
    string SeasonName,
    string Status,
    DateOnly PlantingDate,
    string YieldUnit,
    decimal CultivatedArea);
public sealed record FarmOptions(
    string FarmName,
    DateOnly Today,
    IReadOnlyList<FarmProductOption> Products,
    IReadOnlyList<FarmSeasonOption> Seasons,
    IReadOnlyList<string> Activities);
public sealed record FarmHistory(
    long AuditLogId,
    string Action,
    string? Reason,
    DateTime OccurredAt,
    string? Recorder,
    string? Before,
    string? After);
public sealed record FarmCommitment(string Source, decimal Quantity, string Unit, DateOnly? DeliveryDate);
public sealed record SeasonResponse(
    long CropSeasonId,
    string SeasonName,
    long ProductId,
    string ProductName,
    string CropVariety,
    decimal CultivatedArea,
    DateOnly PlantingDate,
    DateOnly EstimatedHarvestDate,
    DateOnly? ActualHarvestDate,
    decimal EstimatedYield,
    decimal? ActualYield,
    string YieldUnit,
    string Status,
    decimal RecordedHarvestYield,
    int LogCount,
    IReadOnlyList<FarmCommitment> Commitments,
    IReadOnlyList<FarmHistory> History,
    string Version,
    string? Message = null)
{
    public IReadOnlyList<SeasonActivity> Activities { get; init; } = [];

    public IReadOnlyList<SeasonHarvest> HarvestBatches { get; init; } = [];
}

public sealed record SeasonActivity(
    long CultivationLogId,
    DateTime ActionDate,
    string ActionType,
    string? Description,
    long? CorrectsLogId,
    string? CorrectionReason);
public sealed record SeasonHarvest(
    long HarvestBatchId,
    string BatchCode,
    DateOnly? ActualHarvestDate,
    decimal Quantity,
    string Unit);
public sealed record FarmingLogResponse(
    long CultivationLogId,
    long CropSeasonId,
    string SeasonName,
    DateOnly ActionDate,
    string ActionType,
    string? DetailDescription,
    string? InputMaterial,
    string? Dosage,
    string? PerformedBy,
    string Recorder,
    DateTime RecordedAt,
    IReadOnlyList<string> EvidenceUrls,
    long? CorrectsLogId,
    string? CorrectionReason,
    string Version,
    string? Message = null);
public sealed record BatchResponse(
    long HarvestBatchId,
    string BatchCode,
    long ProductId,
    string ProductName,
    long CropSeasonId,
    string SeasonName,
    string FarmName,
    string Unit,
    DateOnly? EstimatedHarvestDate,
    DateOnly? ActualHarvestDate,
    DateOnly? ExpirationDate,
    decimal EstimatedYield,
    decimal HarvestYield,
    decimal AdjustmentQuantity,
    decimal ReservedQuantity,
    decimal ShippedQuantity,
    decimal AvailableQuantity,
    string Status,
    string? QualityNotes,
    int LogCount,
    string? QrIdentity,
    string? TraceabilityUrl,
    bool QrEligible,
    IReadOnlyList<FarmCommitment> Commitments,
    IReadOnlyList<FarmHistory> History,
    string Version,
    string? Message = null);
public sealed record FarmLabelResponse(
    long HarvestBatchId,
    string BatchCode,
    string ProductName,
    string FarmName,
    string SeasonName,
    DateOnly? HarvestDate,
    DateOnly? ExpirationDate,
    string QrIdentity,
    string TraceabilityUrl,
    string QrPng,
    string QrSvg,
    string Message);
