namespace AgriSustain.Application.DTOs.Administration;

/// <summary>UC-ADM-01 date range in Vietnam dates; empty means the last 30 days.</summary>
public sealed record PlatformDashboardQuery(DateOnly? From = null, DateOnly? To = null);

/// <summary>BR-DB-02: rates are null when nothing reached the state that divides them.</summary>
public sealed record OrderStatsDto(
    int Total,
    int DeliveryFinished,
    int Delivered,
    decimal? DeliverySuccessRate,
    int Cancelled,
    decimal? CancellationRate);

public sealed record NewUsersDto(int RetailCustomer, int Farmer, int WholesaleBuyer)
{
    public int Total => RetailCustomer + Farmer + WholesaleBuyer;
}

/// <summary>One unfinished item of an operational role (BR-DB-03); the Administrator only views it.</summary>
public sealed record PendingWorkItemDto(
    string Type,
    long ReferenceId,
    string Reference,
    string SubmittedBy,
    DateTime SubmittedAt,
    double WaitHours,
    bool SlaWarning,
    string Status,
    string ResponsibleRole,
    string UseCase);

public sealed record PendingWorkSummaryDto(int Total, int OverSla);

/// <summary>
/// A section that could not be loaded is null and named in <see cref="Unavailable"/>, so the screen
/// never shows it as zero (MSG-DB-03).
/// </summary>
public sealed record PlatformDashboardDto(
    DateOnly From,
    DateOnly To,
    DateTimeOffset UpdatedAt,
    decimal? Gmv,
    OrderStatsDto? Orders,
    NewUsersDto? NewUsers,
    PendingWorkSummaryDto? PendingSummary,
    IReadOnlyList<PendingWorkItemDto>? PendingWork,
    IReadOnlyList<string> Unavailable);

/// <summary>Row read by the repository before wait time and SLA are worked out.</summary>
public sealed record PendingWorkRow(
    string Type,
    long ReferenceId,
    string Reference,
    string SubmittedBy,
    DateTime SubmittedAt,
    string Status);
