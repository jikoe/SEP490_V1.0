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
    public const int DefaultDays = 30;
    public const int MaxMonths = 12;
    public const int MaxPendingRows = 200;
    public static readonly TimeSpan SlaLimit = TimeSpan.FromHours(24);

    public const string SectionGmv = "Gmv";
    public const string SectionOrders = "Orders";
    public const string SectionNewUsers = "NewUsers";
    public const string SectionPendingWork = "PendingWork";

    // MSG-DB-01
    public const string InvalidRangeMessage =
        "Khoảng thời gian không hợp lệ. Hãy chọn ngày kết thúc từ ngày bắt đầu trở đi và không quá 12 tháng.";

    // MSG-DB-04
    public const string AlreadyProcessedMessage = "Mục này đã được xử lý. Danh sách đã được tải lại.";

    private static readonly IReadOnlyDictionary<string, (string Role, string UseCase)> s_owners =
        new Dictionary<string, (string, string)>
        {
            ["Kyc"] = ("Verification Officer", "UC-VO-01"),
            ["Listing"] = ("Content Moderator", "UC-CM-01"),
            ["Complaint"] = ("Customer Support", "UC-CS-01"),
            ["Refund"] = ("Finance Staff", "UC-FIN-03")
        };

    private readonly ILogger _logger = logger ?? NullLogger<PlatformDashboardService>.Instance;

    public static IReadOnlyCollection<string> PendingWorkTypes => s_owners.Keys.ToList();

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

    /// <summary>BR-DB-03: wait time = now − submission time; over 24 hours is an SLA warning.</summary>
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

    private async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await administration.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");
}
