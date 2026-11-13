using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriSustain.Application.Services.Administration;

/// <summary>
/// UC-ADM-01 View Platform Dashboard and Pending Work. Everything is read-only: the Administrator
/// sees the queues of the operational roles but never acts on them (BR-DB-03).
/// </summary>
public sealed class PlatformDashboardService(
    IPlatformDashboardRepository repository,
    IFinanceRepository finance,
    IAdministrationRepository administration,
    IActorContext actorContext,
    TimeProvider clock,
    ILogger<PlatformDashboardService>? logger = null) : IPlatformDashboardService
{
    /// <summary>
    /// Length of the default period when no dates are given.
    /// </summary>
    public const int DefaultDays = 30;
    /// <summary>
    /// The period must be shorter than this many months (BR-DB-02).
    /// </summary>
    public const int MaxMonths = 12;
    /// <summary>
    /// At most this many pending items are returned; the summary still counts all of them.
    /// </summary>
    public const int MaxPendingRows = 200;
    /// <summary>
    /// Waiting longer than this marks an item as an SLA warning (BR-DB-03).
    /// </summary>
    public static readonly TimeSpan SlaLimit = TimeSpan.FromHours(24);

    /// <summary>
    /// Section names reported in Unavailable when a section fails (MSG-DB-03).
    /// </summary>
    public const string SectionGmv = "Gmv";
    public const string SectionOrders = "Orders";
    public const string SectionNewUsers = "NewUsers";
    public const string SectionPendingWork = "PendingWork";

    /// <summary>
    /// MSG-DB-01: invalid period.
    /// </summary>
    public const string InvalidRangeMessage =
        "Khoảng thời gian không hợp lệ. Hãy chọn ngày kết thúc từ ngày bắt đầu trở đi và không quá 12 tháng.";

    /// <summary>
    /// MSG-DB-04: the item was processed meanwhile; the list is reloaded.
    /// </summary>
    public const string AlreadyProcessedMessage = "Mục này đã được xử lý. Danh sách đã được tải lại.";

    /// <summary>
    /// Who works each queue and in which use case; also the list of valid pending-work types.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Role, string UseCase)> s_owners =
        new Dictionary<string, (string, string)>
        {
            ["Kyc"] = ("Verification Officer", "UC-VO-01"),
            ["Listing"] = ("Content Moderator", "UC-CM-01"),
            ["Complaint"] = ("Customer Support", "UC-CS-01"),
            ["Refund"] = ("Finance Staff", "UC-FIN-03")
        };

    private readonly ILogger _logger = logger ?? NullLogger<PlatformDashboardService>.Instance;

    /// <summary>
    /// Valid pending-work types: Kyc, Listing, Complaint and Refund.
    /// </summary>
    public static IReadOnlyCollection<string> PendingWorkTypes => s_owners.Keys.ToList();

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
    public async Task<UseCaseResult<PlatformDashboardDto>> GetAsync(PlatformDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<PlatformDashboardDto>();
        }
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime.AddHours(7));
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-(DefaultDays - 1));
        // BR-DB-02: the range ends on or after its start and covers at most 12 months.
        if (to < from || to >= from.AddMonths(MaxMonths) || from.Year < 2000 || to.Year > 9998)
        {
            return UseCaseResult<PlatformDashboardDto>.Validation(new Dictionary<string, string[]>
            {
                ["To"] = [InvalidRangeMessage]
            });
        }
        var fromUtc = FinanceQueryValidator.Utc(from);
        var untilUtc = FinanceQueryValidator.Utc(to.AddDays(1));
        var unavailable = new List<string>();

        // BR-DB-01: GMV comes from the same query as the Finance Dashboard (UC-FIN-01).
        // decimal? so that an unavailable GMV stays null instead of becoming 0.
        var gmv = await TryAsync<decimal?>(SectionGmv, unavailable, async () => await finance.MetricAsync(
            "Gmv", new FinancePeriod(from, to, fromUtc, untilUtc, "Range"), cancellationToken));
        var orders = await TryAsync(SectionOrders, unavailable,
            () => repository.OrderStatsAsync(fromUtc, untilUtc, cancellationToken));
        var newUsers = await TryAsync(SectionNewUsers, unavailable,
            () => repository.NewUsersAsync(fromUtc, untilUtc, cancellationToken));
        var rows = await TryAsync(SectionPendingWork, unavailable,
            () => repository.PendingWorkAsync(cancellationToken));

        List<PendingWorkItemDto>? items = rows?.Select(row => ToItem(row, now.UtcDateTime))
            .OrderByDescending(item => item.WaitHours).ToList();
        var summary = items is null ? null
            : new PendingWorkSummaryDto(items.Count, items.Count(item => item.SlaWarning));
        return UseCaseResult<PlatformDashboardDto>.Success(new PlatformDashboardDto(from, to, now,
            gmv, orders, newUsers, summary, items?.Take(MaxPendingRows).ToList(), unavailable));
    }

    /// <summary>
    /// SD2. Re-reads one pending item when the Administrator opens it. An unknown type or a non-positive id is 404; an
    /// item that was processed meanwhile is 409 ALREADY_PROCESSED (MSG-DB-04) so the screen reloads. Read-only: the
    /// Administrator never acts on another role's queue (BR-DB-03).
    /// </summary>
    /// <param name="type">Kyc, Listing, Complaint or Refund.</param>
    /// <param name="referenceId">Id of the profile, product, complaint or refund.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The item with wait time, SLA flag, responsible role and use case; or 403 / 404 / 409.</returns>
    public async Task<UseCaseResult<PendingWorkItemDto>> GetPendingWorkAsync(string type, long referenceId,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<PendingWorkItemDto>();
        }
        if (!s_owners.ContainsKey(type) || referenceId <= 0)
        {
            return UseCaseResult<PendingWorkItemDto>.Failure("NOT_FOUND", "Không tìm thấy mục cần xem.",
                "NotFound");
        }
        var row = await repository.PendingWorkItemAsync(type, referenceId, cancellationToken);
        return row is null
            ? UseCaseResult<PendingWorkItemDto>.Failure("ALREADY_PROCESSED", AlreadyProcessedMessage, "Conflict")
            : UseCaseResult<PendingWorkItemDto>.Success(ToItem(row, clock.GetUtcNow().UtcDateTime));
    }

    /// <summary>
    /// BR-DB-03: wait time = now − submission time; over 24 hours is an SLA warning.
    /// A submission time in the future counts as 0. Adds the responsible role and use case of the type.
    /// </summary>
    /// <param name="row">Pending row from the repository.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    /// <returns>The item, wait time rounded to 0.1 hour.</returns>
    public static PendingWorkItemDto ToItem(PendingWorkRow row, DateTime nowUtc)
    {
        var wait = nowUtc - row.SubmittedAt;
        if (wait < TimeSpan.Zero)
        {
            wait = TimeSpan.Zero;
        }
        var owner = s_owners[row.Type];
        return new PendingWorkItemDto(row.Type, row.ReferenceId, row.Reference, row.SubmittedBy,
            row.SubmittedAt, Math.Round(wait.TotalHours, 1), wait > SlaLimit, row.Status, owner.Role,
            owner.UseCase);
    }

    /// <summary>
    /// Runs one dashboard section. Any error except cancellation is logged and the section is added to
    /// <paramref name="unavailable"/>, so the rest of the dashboard is still shown (MSG-DB-03).
    /// </summary>
    /// <typeparam name="T">Section result type.</typeparam>
    /// <param name="section">Section name.</param>
    /// <param name="unavailable">Collects the names of failed sections.</param>
    /// <param name="load">Query of the section.</param>
    /// <returns>The section result, or default (null) when it failed.</returns>
    private async Task<T?> TryAsync<T>(string section, List<string> unavailable, Func<Task<T>> load)
    {
        try
        {
            return await load();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // MSG-DB-03: the rest of the dashboard is still shown.
            _logger.LogError(exception, "Platform dashboard section unavailable: {Section}", section);
            unavailable.Add(section);
            return default;
        }
    }

    /// <summary>
    /// Checks the caller in the database: an Active, unlocked SystemAdmin / Administrator / Admin.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when allowed.</returns>
    private async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await administration.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    /// <summary>
    /// 403 FORBIDDEN.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");
}
