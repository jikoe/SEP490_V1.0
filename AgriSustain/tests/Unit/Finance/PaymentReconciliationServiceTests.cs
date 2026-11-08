using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.Finance;

/// <summary>UC-FIN-02 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-FIN-02")]
public sealed class PaymentReconciliationServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly FinanceStore _store = new();

    private FinanceService Service() => new(_store, new FinanceActor(), _store.Clock,
        NullLogger<FinanceService>.Instance);

    // ---------- UC-FIN-02: FinanceService.SearchAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #001] Forward all validated search filters to repository")]
    public async Task SearchAsync_SearchCriteria_ForwardsValidatedFilters()
    {
        var query = new FinancePaymentQuery(Provider: "PayOS", Status: "Pending", Page: 2);
        var response = await Service().SearchAsync(query, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(query, _store.LastSearch);
    }

    [Fact(DisplayName = "[UC-FIN-02 #002] Return 403 without searching")]
    public async Task SearchAsync_AccessDenied_RejectsBeforeSearch()
    {
        _store.Allowed = false;
        var response = await Service().SearchAsync(new(), default);

        Assert.Equal(403, response.StatusCode);
        Assert.Null(_store.LastSearch);
    }

    // ---------- UC-FIN-02: FinanceQueryValidator.IsValid ----------

    [Fact(DisplayName = "[UC-FIN-02 #003] Reject reversed date range")]
    public void IsValid_InvalidDates_RejectsReversedDates() => Assert.False(FinanceQueryValidator.IsValid(new FinancePaymentQuery(From: new(2026, 11, 2), To: new(2026, 11, 1))));

    [Fact(DisplayName = "[UC-FIN-02 #004] Accept 150 characters and reject 151")]
    public void IsValid_SearchLength_ChecksKeywordLimit()
    {
        Assert.True(FinanceQueryValidator.IsValid(new FinancePaymentQuery(Search: new string('a', 150))));
        Assert.False(FinanceQueryValidator.IsValid(new FinancePaymentQuery(Search: new string('a', 151))));
    }

    [Fact(DisplayName = "[UC-FIN-02 #005] Reject unsupported filter values")]
    public void IsValid_UnknownFilters_RejectsUnknownValues()
    {
        Assert.False(FinanceQueryValidator.IsValid(new FinancePaymentQuery(Category: "Other")));
        Assert.False(FinanceQueryValidator.IsValid(new FinancePaymentQuery(Provider: "Unknown")));
        Assert.False(FinanceQueryValidator.IsValid(new FinancePaymentQuery(Status: "Unknown")));
    }

    // ---------- UC-FIN-02: FinanceService.DetailAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #006] Return related order, unpaid balance, version and read-only real reconciliation")]
    public async Task DetailAsync_Detail_ReturnsLinkedObligation()
    {
        _store.Payment = FinanceStore.OrderPayment();
        var result = await Service().DetailAsync(1, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("UT-ORDER", result.Value!.Obligation!.Reference);
        Assert.Equal(100000, result.Value.Obligation.Unpaid);
        Assert.Equal(64, result.Value.Version.Length);
        Assert.False(result.Value.CanReconcile);
        Assert.NotEmpty(result.Value.UnavailableReason!);
    }

    [Fact(DisplayName = "[UC-FIN-02 #007] Return 404")]
    public async Task DetailAsync_MissingDetail_ReturnsNotFound()
    {
        var response = await Service().DetailAsync(99, default);

        Assert.Equal(404, response.StatusCode);
    }

    // ---------- UC-FIN-02: FinanceService.ReconcileAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #008] Reject direct reconciliation and retain real payment status")]
    public async Task ReconcileAsync_RealReconciliationDeferred_PreservesRealPayment()
    {
        _store.Payment = FinanceStore.OrderPayment();
        var detail = (await Service().DetailAsync(1, default)).Value!;
        var response = await Service().ReconcileAsync(1, new(detail.Version, Guid.NewGuid().ToString("D")), default);

        Assert.False(response.IsSuccess);
        Assert.Equal("Pending", _store.Payment.PaymentStatus);
    }

    // ---------- UC-FIN-02: FinancePaymentRules.Unpaid ----------

    [Fact(DisplayName = "[UC-FIN-02 #009] Subtract only captured payments with paid timestamps")]
    public void Unpaid_Unpaid_CountsOnlyCapturedPayments()
    {
        var p = FinanceStore.OrderPayment(); p.Amount = 40000;
        p.Order!.PaymentTransactions.Add(new() { Amount = 60000, PaymentStatus = "Succeeded", PaidAt = new DateTime(2026, 11, 1) });

        Assert.Equal(40000, FinancePaymentRules.Unpaid(p));
    }

    [Fact(DisplayName = "[UC-FIN-02 #010] Clamp unpaid balance to zero")]
    public void Unpaid_Overpayment_ClampsToZero()
    {
        var p = FinanceStore.OrderPayment(); p.Amount = 120000; p.PaymentStatus = "Succeeded"; p.PaidAt = new DateTime(2026, 11, 1);

        Assert.Equal(0, FinancePaymentRules.Unpaid(p));
    }

    // ---------- UC-FIN-02: FinancePaymentRules.IsCaptured ----------

    [Fact(DisplayName = "[UC-FIN-02 #011] Do not count the payment as captured")]
    public void IsCaptured_MissingPaidAt_ExcludesUnverifiedPayment()
    {
        var p = FinanceStore.OrderPayment(); p.PaymentStatus = "Succeeded";

        Assert.False(FinancePaymentRules.IsCaptured(p));
    }

    // ---------- UC-FIN-02: FinancePaymentRules.Version ----------

    [Fact(DisplayName = "[UC-FIN-02 #012] Change concurrency version when financial values change")]
    public void Version_VersionChanges_ChangesConcurrencyToken()
    {
        var p = FinanceStore.OrderPayment();
        var version = FinancePaymentRules.Version(p, []);
        p.Amount++;

        Assert.NotEqual(version, FinancePaymentRules.Version(p, []));
    }

    // ---------- UC-FIN-02: FinanceQueryValidator.IsValid ----------

    [Fact(DisplayName = "[UC-FIN-02 #023] Reject invalid concurrency and idempotency tokens")]
    public void IsValid_InvalidTokens_RejectsInvalidTokens()
    {
        Assert.False(FinanceQueryValidator.IsValid(new ReconcilePaymentRequest(new string('Z', 64), Guid.NewGuid().ToString("D"))));
        Assert.False(FinanceQueryValidator.IsValid(new ReconcilePaymentRequest(new string('A', 64), "bad-key")));
    }

    // ---------- Test doubles ----------

    private sealed class FinanceActor(long id = 90) : ICurrentActor
    {
        public long UserId => id;
        public string Role => "FinanceStaff";
    }

    private sealed class FinanceStore : IFinanceRepository
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
    }
}
