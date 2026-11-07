using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Administration;

/// <summary>UC-ADM-01 View Platform Dashboard and Pending Work.</summary>
public sealed class PlatformDashboardServiceTests
{
    // 10:00 on 07/11/2026 in Vietnam.
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 3, 0, 0, TimeSpan.Zero);

    private readonly AdministrationServiceTests.AdminStore _admins = new();
    private readonly DashboardStore _store = new();
    private readonly FinanceStore _finance = new();

    public PlatformDashboardServiceTests()
    {
        _admins.Users[1] = new User { UserId = 1, Role = "Administrator", Status = "Active" };
        _admins.Users[2] = new User { UserId = 2, Role = "FinanceStaff", Status = "Active" };
    }

    private PlatformDashboardService Service(long actor = 1) =>
        new(_store, _finance, _admins, new Actor(actor), new FakeClock(T0));

    [Fact(DisplayName = "UTC-ADM01-01 Default range is the last 30 days and GMV uses the Finance definition (BR-DB-01)")]
    public async Task Default_Last30Days_FinanceGmv()
    {
        _finance.Gmv = 1_250_000m;

        var result = await Service().GetAsync(new(), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((new DateOnly(2026, 10, 9), new DateOnly(2026, 11, 7)), (result.Value!.From, result.Value.To));
        Assert.Equal(1_250_000m, result.Value.Gmv);
        Assert.Equal("Gmv", _finance.LastMetric);
        // Vietnam midnight of the first day, exclusive end the day after the last one.
        Assert.Equal(new DateTime(2026, 10, 8, 17, 0, 0), _finance.LastPeriod!.From);
        Assert.Equal(new DateTime(2026, 11, 7, 17, 0, 0), _finance.LastPeriod.Until);
        Assert.Empty(result.Value.Unavailable);
    }

    [Theory(DisplayName = "UTC-ADM01-02 An end before the start or a range over 12 months shows MSG-DB-01 (BR-DB-02)")]
    [InlineData("2026-11-07", "2026-11-01")]
    [InlineData("2025-11-07", "2026-11-07")]
    public async Task InvalidRange_MsgDb01(string from, string to)
    {
        var result = await Service().GetAsync(new(DateOnly.Parse(from), DateOnly.Parse(to)), default);

        Assert.Equal(PlatformDashboardService.InvalidRangeMessage, result.Error!.Fields!["To"][0]);
        Assert.Null(_finance.LastMetric);
    }

    [Fact(DisplayName = "UTC-ADM01-03 A range of exactly 12 months is accepted")]
    public async Task TwelveMonths_Accepted()
    {
        var result = await Service().GetAsync(new(new DateOnly(2025, 11, 8), new DateOnly(2026, 11, 7)), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact(DisplayName = "UTC-ADM01-04 Pending work is ordered by wait time and over 24 hours is an SLA warning (BR-DB-03)")]
    public async Task PendingWork_WaitAndSla()
    {
        _store.Rows.Add(new("Listing", 5, "Sản phẩm · Cải", "Nông trại A", T0.UtcDateTime.AddHours(-3), "Pending"));
        _store.Rows.Add(new("Refund", 9, "Hoàn tiền · REF-9", "Khách B", T0.UtcDateTime.AddHours(-30), "Pending"));

        var result = await Service().GetAsync(new(), default);

        var items = result.Value!.PendingWork!;
        Assert.Equal(new long[] { 9, 5 }, items.Select(item => item.ReferenceId));
        Assert.True(items[0].SlaWarning);
        Assert.Equal(30, items[0].WaitHours);
        Assert.Equal(("Finance Staff", "UC-FIN-03"), (items[0].ResponsibleRole, items[0].UseCase));
        Assert.False(items[1].SlaWarning);
        Assert.Equal(new PendingWorkSummaryDto(2, 1), result.Value.PendingSummary);
    }

    [Fact(DisplayName = "UTC-ADM01-05 A failed section is reported, not shown as 0, and the rest still loads (MSG-DB-03)")]
    public async Task PartialFailure_NullNotZero()
    {
        _finance.Fail = true;
        _store.FailUsers = true;
        _store.Stats = new OrderStatsDto(10, 4, 3, 75m, 1, 10m);

        var result = await Service().GetAsync(new(), default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Gmv);
        Assert.Null(result.Value.NewUsers);
        Assert.Equal(10, result.Value.Orders!.Total);
        Assert.Equal(new[] { PlatformDashboardService.SectionGmv, PlatformDashboardService.SectionNewUsers },
            result.Value.Unavailable);
    }

    [Fact(DisplayName = "UTC-ADM01-06 An item already processed by its role shows MSG-DB-04")]
    public async Task ProcessedItem_MsgDb04()
    {
        var result = await Service().GetPendingWorkAsync("Complaint", 4, default);

        Assert.Equal(("ALREADY_PROCESSED", "Conflict"), (result.Error!.Code, result.Error.Kind));
        Assert.Equal(PlatformDashboardService.AlreadyProcessedMessage, result.Error.Message);
    }

    [Fact(DisplayName = "UTC-ADM01-07 A pending item opens read-only with its role and use case")]
    public async Task PendingItem_Detail()
    {
        _store.Rows.Add(new("Kyc", 12, "Hồ sơ nông trại · Đồi Chè", "Nguyễn A", T0.UtcDateTime.AddHours(-25), "PendingApproval"));

        var result = await Service().GetPendingWorkAsync("Kyc", 12, default);

        Assert.Equal(("Verification Officer", "UC-VO-01", true),
            (result.Value!.ResponsibleRole, result.Value.UseCase, result.Value.SlaWarning));
    }

    [Fact(DisplayName = "UTC-ADM01-08 Only an Active Administrator can open the dashboard")]
    public async Task NonAdministrator_Forbidden()
    {
        var result = await Service(actor: 2).GetAsync(new(), default);

        Assert.Equal("Forbidden", result.Error!.Kind);
        Assert.Null(_finance.LastMetric);
    }

    private sealed record Actor(long UserId) : IActorContext;

    private sealed class DashboardStore : IPlatformDashboardRepository
    {
        public List<PendingWorkRow> Rows { get; } = [];
        public OrderStatsDto Stats { get; set; } = new(0, 0, 0, null, 0, null);
        public bool FailUsers { get; set; }

        public Task<OrderStatsDto> OrderStatsAsync(DateTime from, DateTime until, CancellationToken ct) =>
            Task.FromResult(Stats);

        public Task<NewUsersDto> NewUsersAsync(DateTime from, DateTime until, CancellationToken ct) =>
            FailUsers ? throw new InvalidOperationException("db down") : Task.FromResult(new NewUsersDto(1, 2, 0));

        public Task<IReadOnlyList<PendingWorkRow>> PendingWorkAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PendingWorkRow>>(Rows);

        public Task<PendingWorkRow?> PendingWorkItemAsync(string type, long referenceId, CancellationToken ct) =>
            Task.FromResult(Rows.FirstOrDefault(row => row.Type == type && row.ReferenceId == referenceId));
    }

    private sealed class FinanceStore : IFinanceRepository
    {
        public decimal Gmv { get; set; }
        public bool Fail { get; set; }
        public string? LastMetric { get; private set; }
        public FinancePeriod? LastPeriod { get; private set; }

        public Task<decimal> MetricAsync(string metric, FinancePeriod period, CancellationToken ct)
        {
            LastMetric = metric;
            LastPeriod = period;
            return Fail ? throw new InvalidOperationException("finance down") : Task.FromResult(Gmv);
        }

        public Task<bool> CanAccessAsync(long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResponse<FinanceRecord>> RecordsAsync(string metric, FinancePeriod period, int page,
            int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResponse<FinancePaymentRow>> SearchAsync(FinancePaymentQuery query, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<PaymentTransaction?> PaymentAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditLog>> HistoryAsync(long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Invoice>> InvoicesAsync(PaymentTransaction payment, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
