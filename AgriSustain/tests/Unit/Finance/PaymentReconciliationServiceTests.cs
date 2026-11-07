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

/// <summary>UC-FIN-02 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-FIN-02")]
public sealed class PaymentReconciliationServiceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private readonly FinanceStore _store = new();

    private FinanceService Service() => new(_store, new FinanceActor(), _store.Clock,
        NullLogger<FinanceService>.Instance);
    private FinanceSampleService Sample(long actor = 90) =>
        new(_store, new FinanceActor(actor), _store.Cache, _store.Clock);
    public void Dispose() => _store.Dispose();

    private static async Task<ReconcilePaymentRequest> Request(FinanceSampleService service, long id) =>
        new((await service.DetailAsync(id, default)).Value!.Version, Guid.NewGuid().ToString("D"));

    // ---------- UC-FIN-02: FinanceService.SearchAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #001] Forward all validated search filters to repository")]
    public async Task SearchAsync_SearchCriteria_ForwardsValidatedFilters()
    {
        var query = new FinancePaymentQuery(Provider: "SePay", Status: "AmountMismatch", Page: 2);
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

    [Fact(DisplayName = "[UC-FIN-02 #025] Compare the saved SePay credit with the linked order amount")]
    public async Task DetailAsync_SepayUnderpayment_UsesStoredReceiptAndFlagsReview()
    {
        var payment = FinanceStore.OrderPayment();
        payment.GatewayProvider = "SePay";
        payment.TransactionCode = "SEPAY-12";
        payment.PaymentPurpose = "RetailOrderPayment";
        payment.PaymentStatus = "AmountMismatch";
        payment.Amount = 90000;
        payment.WebhookPayload = """{"id":12,"transferType":"in","transferAmount":90000,"referenceCode":"BANK-123"}""";
        _store.Payment = payment;

        var result = await Service().DetailAsync(1, default);

        Assert.True(result.Value!.Payment.NeedsReview);
        Assert.Equal(100000m, result.Value.ExpectedAmount);
        Assert.Equal(90000m, result.Value.LastReceipt!.Amount);
        Assert.Equal("BANK-123", result.Value.LastReceipt.Reference);
        Assert.Equal(100000m, result.Value.Obligation!.Unpaid);
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

    // ---------- UC-FIN-02: FinanceSampleService.SearchAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #013] Return the three matching sample subscriptions")]
    public async Task SearchAsync_SampleFilters_ReturnsMatchingRows()
    {
        var result = await Sample().SearchAsync(new(Category: "Subscription", Provider: "PayOS"), default);

        Assert.Equal(3, result.Value!.Total);
        Assert.All(result.Value.Items, x =>
        {
            Assert.Equal("Subscription", x.Category);
            Assert.Equal("PayOS", x.Provider);
        });
    }

    [Fact(DisplayName = "[UC-FIN-02 #014] Return three different rows and preserve total eight")]
    public async Task SearchAsync_SamplePaging_ReturnsDistinctPages()
    {
        var s = Sample();
        var first = (await s.SearchAsync(new(PageSize: 3), default)).Value!;
        var second = (await s.SearchAsync(new(Page: 2, PageSize: 3), default)).Value!;

        Assert.Equal(8, second.Total);
        Assert.Equal(3, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
    }

    // ---------- UC-FIN-02: FinanceSampleService.ReconcileAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #015] Mark sample paid, clear obligation balance and record one history entry")]
    public async Task ReconcileAsync_SampleSuccess_RecordsSuccess()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(1, await Request(s, 1), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value!.Outcome);
        Assert.Equal(0, result.Value.Detail.Obligation!.Unpaid);
        Assert.Equal("Paid", result.Value.Detail.Obligation.Status);
        Assert.NotNull(result.Value.Detail.Payment.PaidAt);
        Assert.Single(result.Value.Detail.History);
        Assert.False(result.Value.Detail.CanReconcile);
    }

    [Fact(DisplayName = "[UC-FIN-02 #016] Record mismatch, preserve Pending and unpaid amount, require review")]
    public async Task ReconcileAsync_SampleMismatch_PreservesUnpaidMismatch()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(2, await Request(s, 2), default);

        Assert.Equal("Mismatch", result.Value!.Outcome);
        Assert.Equal("Pending", result.Value.Detail.Payment.Status);
        Assert.Equal(455000, result.Value.Detail.Obligation!.Unpaid);
        Assert.True(result.Value.Detail.Payment.NeedsReview);
    }

    [Fact(DisplayName = "[UC-FIN-02 #017] Record late payment for manual review without paying the obligation")]
    public async Task ReconcileAsync_SampleLatePayment_FlagsLatePayment()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(7, await Request(s, 7), default);

        Assert.Equal("LatePayment", result.Value!.Outcome);
        Assert.Equal("Pending", result.Value.Detail.Payment.Status);
        Assert.True(result.Value.Detail.Payment.NeedsReview);
        Assert.Equal("Cancelled", result.Value.Detail.Obligation!.Status);
    }

    [Fact(DisplayName = "[UC-FIN-02 #018] Record Failed and keep obligation unpaid")]
    public async Task ReconcileAsync_SampleFailed_PreservesUnpaidFailure()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(6, await Request(s, 6), default);

        Assert.Equal("Failed", result.Value!.Detail.Payment.Status);
        Assert.Equal(555000, result.Value.Detail.Obligation!.Unpaid);
    }

    [Fact(DisplayName = "[UC-FIN-02 #019] Record Expired without clearing outstanding obligation")]
    public async Task ReconcileAsync_SampleExpired_PreservesUnpaidExpiry()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(5, await Request(s, 5), default);

        Assert.Equal("Expired", result.Value!.Detail.Payment.Status);
        Assert.Equal(999000, result.Value.Detail.Obligation!.Unpaid);
    }

    [Fact(DisplayName = "[UC-FIN-02 #020] Return AlreadyApplied without adding history or money twice")]
    public async Task ReconcileAsync_IdempotentReplay_AvoidsDuplicateCredit()
    {
        var s = Sample();
        var request = await Request(s, 1);
        await s.ReconcileAsync(1, request, default);
        var result = await s.ReconcileAsync(1, request, default);

        Assert.True(result.Value!.AlreadyApplied);
        Assert.Single(result.Value.Detail.History);
        Assert.Equal(0, result.Value.Detail.Obligation!.Unpaid);
    }

    [Fact(DisplayName = "[UC-FIN-02 #021] Reject stale version and preserve one history entry")]
    public async Task ReconcileAsync_StaleVersion_RejectsStaleRequest()
    {
        var s = Sample();
        var request = await Request(s, 2);
        await s.ReconcileAsync(2, request, default);
        var response = await s.ReconcileAsync(2, request with { RequestKey = Guid.NewGuid().ToString("D") }, default);

        Assert.False(response.IsSuccess);
        var response2 = await s.DetailAsync(2, default);

        Assert.Single(response2.Value!.History);
    }

    [Fact(DisplayName = "[UC-FIN-02 #022] Actor 91 still sees original Pending sample")]
    public async Task ReconcileAsync_ActorIsolation_IsolatesSampleLedgers()
    {
        var first = Sample(90);
        var second = Sample(91);
        await first.ReconcileAsync(1, await Request(first, 1), default);
        var response = await second.DetailAsync(1, default);

        Assert.Equal("Pending", response.Value!.Payment.Status);
        var response2 = await second.DetailAsync(1, default);

        Assert.Empty(response2.Value!.History);
    }

    // ---------- UC-FIN-02: FinanceQueryValidator.IsValid ----------

    [Fact(DisplayName = "[UC-FIN-02 #023] Reject invalid concurrency and idempotency tokens")]
    public void IsValid_InvalidTokens_RejectsInvalidTokens()
    {
        Assert.False(FinanceQueryValidator.IsValid(new ReconcilePaymentRequest(new string('Z', 64), Guid.NewGuid().ToString("D"))));
        Assert.False(FinanceQueryValidator.IsValid(new ReconcilePaymentRequest(new string('A', 64), "bad-key")));
    }

    // ---------- UC-FIN-02: FinanceSampleService.ReconcileAsync ----------

    [Fact(DisplayName = "[UC-FIN-02 #024] Activate sample subscription and clear unpaid balance")]
    public async Task ReconcileAsync_SampleSubscription_ActivatesSubscription()
    {
        var s = Sample();
        var result = await s.ReconcileAsync(3, await Request(s, 3), default);

        Assert.Equal("Active", result.Value!.Detail.Obligation!.Status);
        Assert.Equal(0, result.Value.Detail.Obligation.Unpaid);
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
        public Task<decimal?> MetricAsync(string metric, FinancePeriod period, CancellationToken ct)
        {
            MetricCalls.Add(metric);
            if (metric == FailedMetric) throw MetricError ?? new IOException("Metric unavailable");
            return Task.FromResult<decimal?>(metric == "Gmv" ? 1000000m : 10000m);
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
