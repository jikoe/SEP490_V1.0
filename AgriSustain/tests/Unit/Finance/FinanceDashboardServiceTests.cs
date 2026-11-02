using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.Finance;

/// <summary>UC-FIN-01 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-FIN-01")]
public sealed class FinanceDashboardServiceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly FinanceStore _store = new();

    private FinanceService Service() => new(_store, new FinanceActor(), _store.Clock,
        NullLogger<FinanceService>.Instance);
    public void Dispose() => _store.Dispose();

    // ---------- UC-FIN-01: FinanceService.DashboardAsync ----------

    [Fact(DisplayName = "[UC-FIN-01 #001] Return six metrics and pending transaction total")]
    public async Task DashboardAsync_Dashboard_ReturnsMetricsAndPendingTotal()
    {
        var result = await Service().DashboardAsync(new(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value!.Metrics.Count);
        Assert.Equal(1000000m, result.Value.Metrics["Gmv"]);
        Assert.Equal(7, result.Value.TransactionsToProcess);
        Assert.Empty(result.Value.Unavailable);
        Assert.Equal(6, _store.MetricCalls.Count);
    }

    [Fact(DisplayName = "[UC-FIN-01 #002] Return 403 before querying metrics")]
    public async Task DashboardAsync_AccessDenied_RejectsBeforeQuery()
    {
        _store.Allowed = false;
        var response = await Service().DashboardAsync(new(), default);

        Assert.Equal(403, response.StatusCode);
        Assert.Empty(_store.MetricCalls);
    }

    [Fact(DisplayName = "[UC-FIN-01 #003] Reject unsupported period")]
    public async Task DashboardAsync_InvalidPeriod_RejectsPeriod()
    {
        var response = await Service().DashboardAsync(new("Year"), default);

        Assert.False(response.IsSuccess);
        Assert.Empty(_store.MetricCalls);
    }

    // ---------- UC-FIN-01: FinanceQueryValidator.Period ----------

    [Fact(DisplayName = "[UC-FIN-01 #004] Resolve local date to November 1 and UTC boundaries to minus seven hours")]
    public void Period_LocalDay_UsesVietnamDay()
    {
        var p = FinanceQueryValidator.Period(new("Day"), new DateTime(2026, 10, 31, 18, 0, 0, DateTimeKind.Utc))!;

        Assert.Equal(new DateOnly(2026, 11, 1), p.Start);
        Assert.Equal(p.Start, p.End);
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), p.From);
        Assert.Equal(TimeSpan.FromDays(1), p.Until - p.From);
    }

    [Fact(DisplayName = "[UC-FIN-01 #005] Include February 29 in monthly report")]
    public void Period_LeapMonth_IncludesLeapDay()
    {
        var p = FinanceQueryValidator.Period(new("Month", new(2028, 2, 10)), DateTime.UtcNow)!;

        Assert.Equal(new DateOnly(2028, 2, 1), p.Start);
        Assert.Equal(new DateOnly(2028, 2, 29), p.End);
    }

    [Fact(DisplayName = "[UC-FIN-01 #006] Return October 1 through December 31")]
    public void Period_FourthQuarter_ReturnsQuarterBounds()
    {
        var p = FinanceQueryValidator.Period(new("Quarter", new(2026, 12, 31)), DateTime.UtcNow)!;

        Assert.Equal(new DateOnly(2026, 10, 1), p.Start);
        Assert.Equal(new DateOnly(2026, 12, 31), p.End);
        Assert.Equal(FinanceQueryValidator.Utc(new(2027, 1, 1)), p.Until);
    }

    [Fact(DisplayName = "[UC-FIN-01 #007] Reject 1899 and accept 1900")]
    public void Period_MinimumYear_ChecksMinimumYear()
    {
        Assert.Null(FinanceQueryValidator.Period(new("Month", new(1899, 1, 1)), DateTime.UtcNow));
        Assert.NotNull(FinanceQueryValidator.Period(new("Month", new(1900, 1, 1)), DateTime.UtcNow));
    }

    // ---------- UC-FIN-01: FinanceService.DashboardAsync ----------

    [Fact(DisplayName = "[UC-FIN-01 #008] Mark only Commission unavailable without displaying a false zero")]
    public async Task DashboardAsync_PartialFailure_MarksMetricUnavailable()
    {
        _store.FailedMetric = "Commission";
        var result = await Service().DashboardAsync(new(), default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Metrics["Commission"]);
        Assert.Equal(1000000m, result.Value.Metrics["Gmv"]);
        Assert.Equal("Commission", Assert.Single(result.Value.Unavailable));
    }

    [Fact(DisplayName = "[UC-FIN-01 #009] Keep metrics and mark transaction count unavailable")]
    public async Task DashboardAsync_PendingFailure_MarksTransactionsUnavailable()
    {
        _store.SearchError = new IOException("offline");
        var result = await Service().DashboardAsync(new(), default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.TransactionsToProcess);
        Assert.Null(result.Value.PendingTransactions);
        Assert.Contains("Transactions", result.Value.Unavailable);
    }

    [Fact(DisplayName = "[UC-FIN-01 #010] Propagate cancellation instead of returning a partial dashboard")]
    public async Task DashboardAsync_Cancellation_PropagatesCancellation()
    {
        _store.FailedMetric = "Gmv";
        _store.MetricError = new OperationCanceledException();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service().DashboardAsync(new(), default));
    }

    [Fact(DisplayName = "[UC-FIN-01 #011] Request attention-only transactions with page size three and selected dates")]
    public async Task DashboardAsync_PendingCriteria_ForwardsAttentionFilters()
    {
        await Service().DashboardAsync(new("Month", new(2026, 10, 12)), default);

        Assert.True(_store.LastSearch!.NeedsAttention);
        Assert.Equal(3, _store.LastSearch.PageSize);
        Assert.Equal(new DateOnly(2026, 10, 1), _store.LastSearch.From);
        Assert.Equal(new DateOnly(2026, 10, 31), _store.LastSearch.To);
    }

    // ---------- UC-FIN-01: FinanceService.RecordsAsync ----------

    [Fact(DisplayName = "[UC-FIN-01 #012] Forward requested page and return metric records")]
    public async Task RecordsAsync_Records_ReturnsRequestedPage()
    {
        var result = await Service().RecordsAsync(new("Gmv", Page: 2, PageSize: 20), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Page);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal("UT-RECORD", Assert.Single(result.Value.Items).Reference);
    }

    [Fact(DisplayName = "[UC-FIN-01 #013] Reject unsupported drill-down metric")]
    public async Task RecordsAsync_InvalidMetric_RejectsUnknownMetric()
    {
        var response = await Service().RecordsAsync(new("Unknown"), default);

        Assert.False(response.IsSuccess);
    }

    [Fact(DisplayName = "[UC-FIN-01 #014] Reject out-of-range pagination")]
    public async Task RecordsAsync_InvalidPage_RejectsInvalidPagination()
    {
        var response = await Service().RecordsAsync(new("Gmv", Page: 0), default);

        Assert.False(response.IsSuccess);
        var response2 = await Service().RecordsAsync(new("Gmv", PageSize: 101), default);

        Assert.False(response2.IsSuccess);
    }

    // ---------- Test doubles ----------

    private sealed class FinanceActor(long id = 90) : ICurrentActor
    {
        public long UserId => id;
        public string Role => "FinanceStaff";
    }

    private sealed class FinanceStore : IFinanceRepository, IDisposable
    {
        public bool Allowed { get; set; } = true;
        public string? FailedMetric { get; set; }
        public Exception? MetricError { get; set; }
        public Exception? SearchError { get; set; }
        public List<string> MetricCalls { get; } = [];
        public FinancePaymentQuery? LastSearch { get; private set; }
        public PaymentTransaction? Payment { get; set; }
        public List<AuditLog> Audits { get; } = [];
        public FakeClock Clock { get; } = new(T0);
        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());
        public Task<bool> CanAccessAsync(long userId, CancellationToken ct) => Task.FromResult(Allowed);
        public Task<decimal> MetricAsync(string metric, FinancePeriod period, CancellationToken ct)
        {
            MetricCalls.Add(metric);
            if (metric == FailedMetric) throw MetricError ?? new IOException("Metric unavailable");
            return Task.FromResult(metric == "Gmv" ? 1000000m : 10000m);
        }
        public Task<PagedResponse<FinanceRecord>> RecordsAsync(string metric, FinancePeriod period, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult(new PagedResponse<FinanceRecord>([new(1, "UT-RECORD", null, 10000, "Paid", period.From, null)], 1, page, pageSize));
        public Task<PagedResponse<FinancePaymentRow>> SearchAsync(FinancePaymentQuery query, CancellationToken ct)
        {
            LastSearch = query;
            if (SearchError != null) throw SearchError;
            return Task.FromResult(new PagedResponse<FinancePaymentRow>([new() { Id = 1, Status = "Pending" }], 7, query.Page, query.PageSize));
        }
        public Task<PaymentTransaction?> PaymentAsync(long id, CancellationToken ct) => Task.FromResult(Payment?.PaymentTransactionId == id ? Payment : null);
        public Task<IReadOnlyList<AuditLog>> HistoryAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<AuditLog>>(Audits);
        public Task<IReadOnlyList<Invoice>> InvoicesAsync(PaymentTransaction payment, CancellationToken ct) => Task.FromResult<IReadOnlyList<Invoice>>([]);
        public static PaymentTransaction OrderPayment()
        {
            var order = new Order { OrderId = 1, OrderCode = "UT-ORDER", OrderStatus = "PendingPayment", FinalAmount = 100000 };
            var payment = new PaymentTransaction
            {
                PaymentTransactionId = 1,
                OrderId = 1,
                Order = order,
                PaymentPurpose = "OrderPayment",
                TransactionCode = "UT-PAY",
                GatewayProvider = "PayOS",
                ProviderTransactionReference = "POS-1",
                PaymentStatus = "Pending",
                Amount = 100000
            };
            order.PaymentTransactions.Add(payment); return payment;
        }
        public void Dispose() => Cache.Dispose();
    }
}
