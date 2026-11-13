using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-ADM-01 read-only queries. Times are UTC; <c>until</c> is exclusive.</summary>
public interface IPlatformDashboardRepository
{
    /// <summary>
    /// Seller orders created in the period and their cancellation rate, and seller orders whose first final delivery
    /// state falls in the period and their delivery-success rate (BR-DB-02).
    /// </summary>
    /// <param name="from">Start of the period (UTC, inclusive).</param>
    /// <param name="until">End of the period (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts and rates; a rate is null when its divisor is 0.</returns>
    Task<OrderStatsDto> OrderStatsAsync(DateTime from, DateTime until, CancellationToken cancellationToken);

    /// <summary>
    /// Marketplace accounts created in the period, by role.
    /// </summary>
    /// <param name="from">Start of the period (UTC, inclusive).</param>
    /// <param name="until">End of the period (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts per role.</returns>
    Task<NewUsersDto> NewUsersAsync(DateTime from, DateTime until, CancellationToken cancellationToken);

    /// <summary>
    /// KYC, listing reviews, complaints and refunds still waiting for their role (BR-DB-03).
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>All pending rows.</returns>
    Task<IReadOnlyList<PendingWorkRow>> PendingWorkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The item when it is still pending; null when it was processed or does not exist.
    /// </summary>
    /// <param name="type">Kyc, Listing, Complaint or Refund.</param>
    /// <param name="referenceId">Item id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The row or null.</returns>
    Task<PendingWorkRow?> PendingWorkItemAsync(string type, long referenceId,
        CancellationToken cancellationToken);
}

public interface IPlatformDashboardService
{
    /// <summary>
    /// SD1. Steps: 1) the caller must be an Active, unlocked Administrator; 2) the period defaults to the last 30 days
    /// ending today in Vietnam, and must end on or after its start and cover less than 12 months (BR-DB-02, MSG-DB-01);
    /// 3) load four independent sections — GMV (same query as the Finance Dashboard, BR-DB-01), order statistics, new
    /// users by role and the pending work queues; a section that fails is logged, left null and named in Unavailable so
    /// the screen never shows it as 0 (MSG-DB-03); 4) turn each pending row into an item with wait time and SLA flag,
    /// longest wait first, count the total and those over 24 hours, and return at most 200 rows. Read-only.
    /// </summary>
    /// <param name="query">From and To as Vietnam dates; both optional.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The dashboard, or 403 FORBIDDEN / 400 MSG-DB-01.</returns>
    Task<Common.UseCaseResult<PlatformDashboardDto>> GetAsync(PlatformDashboardQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// SD2. Re-reads one pending item when the Administrator opens it. An unknown type or a non-positive id is 404; an
    /// item that was processed meanwhile is 409 ALREADY_PROCESSED (MSG-DB-04) so the screen reloads. Read-only: the
    /// Administrator never acts on another role's queue (BR-DB-03).
    /// </summary>
    /// <param name="type">Kyc, Listing, Complaint or Refund.</param>
    /// <param name="referenceId">Id of the profile, product, complaint or refund.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The item with wait time, SLA flag, responsible role and use case; or 403 / 404 / 409.</returns>
    Task<Common.UseCaseResult<PendingWorkItemDto>> GetPendingWorkAsync(string type, long referenceId,
        CancellationToken cancellationToken);
}
