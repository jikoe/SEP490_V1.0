using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-ADM-01 read-only queries. Times are UTC; <c>until</c> is exclusive.</summary>
public interface IPlatformDashboardRepository
{
    Task<OrderStatsDto> OrderStatsAsync(DateTime from, DateTime until, CancellationToken cancellationToken);

    Task<NewUsersDto> NewUsersAsync(DateTime from, DateTime until, CancellationToken cancellationToken);

    /// <summary>KYC, listing reviews, complaints and refunds still waiting for their role (BR-DB-03).</summary>
    Task<IReadOnlyList<PendingWorkRow>> PendingWorkAsync(CancellationToken cancellationToken);

    /// <summary>The item when it is still pending; null when it was processed or does not exist.</summary>
    Task<PendingWorkRow?> PendingWorkItemAsync(string type, long referenceId,
        CancellationToken cancellationToken);
}

public interface IPlatformDashboardService
{
    Task<Common.UseCaseResult<PlatformDashboardDto>> GetAsync(PlatformDashboardQuery query,
        CancellationToken cancellationToken);

    Task<Common.UseCaseResult<PendingWorkItemDto>> GetPendingWorkAsync(string type, long referenceId,
        CancellationToken cancellationToken);
}
