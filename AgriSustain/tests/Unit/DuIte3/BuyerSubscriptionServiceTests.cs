using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.B2B;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.DuIte3;

/// <summary>UC-WB-08 Manage Buyer Subscription.</summary>
public sealed class BuyerSubscriptionServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 3, 0, 0, DateTimeKind.Utc);
    private readonly FakeSubscriptionRepository _repo = new();
    private readonly FakeQr _qr = new();
    private readonly FakeEmail _email = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(Now));

    private static readonly TierRecord Free = new(1, "Cơ bản", null, 30, 0m, false, false, 5, true);
    private static readonly TierRecord Silver = new(2, "Bạc", null, 30, 500_000m, true, false, 20, true);
    private static readonly TierRecord Gold = new(3, "Vàng", null, 365, 5_000_000m, true, true, null, true);

    public BuyerSubscriptionServiceTests()
    {
        _repo.Tiers.AddRange([Free, Silver, Gold]);
    }

    private BuyerSubscriptionService Service(long userId = 50) =>
        new(_repo, _qr, _email, new FakeUnitOfWork(), new FakeCurrentUser(userId, "WholesaleBuyer"), _clock);

    private SubscriptionRecord Active(TierRecord tier, DateTime start, DateTime end, bool autoRenew = false) =>
        _repo.Seed(tier, SubscriptionStatuses.Active, start, end, autoRenew);

    private static IncomingBankTransfer Transfer(string reference, decimal amount, long id = 1) =>
        new("SePay", $"SEPAY-{id}", amount, null, $"FT{id}", "{}", [null, $"CK {reference} thanh toan", null]);

    // ---------- Decide (Step 10 date rules) ----------

    [Fact(DisplayName = "UTC-WB08-01 First subscription starts now and ends after the tier duration")]
    public void Decide_New()
    {
        var change = SubscriptionRules.Decide(null, Silver, Now);

        Assert.Equal(SubscriptionChangeKind.New, change.Kind);
        Assert.Equal(Now, change.StartDate);
        Assert.Equal(Now.AddDays(30), change.EndDate);
    }

    [Fact(DisplayName = "UTC-WB08-02 Renewing the same tier inside 7 days keeps the remaining days")]
    public void Decide_Renew()
    {
        var active = Active(Silver, Now.AddDays(-25), Now.AddDays(5));

        var change = SubscriptionRules.Decide(active, Silver, Now);

        Assert.Equal(SubscriptionChangeKind.Renew, change.Kind);
        Assert.Equal(Now.AddDays(5), change.StartDate);
        Assert.Equal(Now.AddDays(35), change.EndDate);
    }

    [Fact(DisplayName = "UTC-WB08-03 Same tier with more than 7 days left is not a renewal (AF7)")]
    public void Decide_SameTier() =>
        Assert.Equal(SubscriptionChangeKind.SameTier,
            SubscriptionRules.Decide(Active(Silver, Now.AddDays(-5), Now.AddDays(25)), Silver, Now).Kind);

    [Fact(DisplayName = "UTC-WB08-04 Higher price upgrades now; lower price is scheduled from EndDate")]
    public void Decide_UpgradeDowngrade()
    {
        var active = Active(Silver, Now.AddDays(-5), Now.AddDays(25));

        var up = SubscriptionRules.Decide(active, Gold, Now);
        var down = SubscriptionRules.Decide(active, Free, Now);

        Assert.Equal(SubscriptionChangeKind.Upgrade, up.Kind);
        Assert.Equal(Now, up.StartDate);
        Assert.Equal(SubscriptionChangeKind.Downgrade, down.Kind);
        Assert.Equal(Now.AddDays(25), down.StartDate);
        Assert.Equal(Now.AddDays(55), down.EndDate);
    }

    [Theory(DisplayName = "UTC-WB08-05 Payment reference is read from the transfer content")]
    [InlineData("CK SUB4F9A0C21B7 thanh toan", "SUB4F9A0C21B7")]
    [InlineData("sub-4f9a0c21b7", "SUB4F9A0C21B7")]
    [InlineData("RC-1234", null)]
    public void FindReference(string text, string? expected) =>
        Assert.Equal(expected, SubscriptionRules.FindReference([text]));

    // ---------- Overview ----------

    [Fact(DisplayName = "UTC-WB08-06 Overview lists active tiers by price with the action for each")]
    public async Task Overview_Tiers()
    {
        Active(Silver, Now.AddDays(-5), Now.AddDays(25));

        var view = (await Service().GetOverviewAsync(default)).Value!;

        Assert.Equal("Bạc", view.Current!.PackageName);
        Assert.Equal(new[] { "Downgrade", "Current", "Upgrade" }, view.Tiers.Select(t => t.Action));
    }

    [Fact(DisplayName = "UTC-WB08-07 No active tier shows the empty-state message (AF1)")]
    public async Task Overview_NoTier()
    {
        _repo.Tiers.Clear();

        var view = (await Service().GetOverviewAsync(default)).Value!;

        Assert.Empty(view.Tiers);
        Assert.Equal(SubscriptionRules.NoTier, view.EmptyMessage);
    }

    [Fact(DisplayName = "UTC-WB08-08 Expired subscription is closed and MSG37 is shown (AF11)")]
    public async Task Overview_Expired()
    {
        var old = Active(Silver, Now.AddDays(-40), Now.AddDays(-10));

        var view = (await Service().GetOverviewAsync(default)).Value!;

        Assert.Null(view.Current);
        Assert.True(view.IsExpired);
        Assert.Equal(SubscriptionRules.Msg37, view.Message);
        Assert.Equal(SubscriptionStatuses.Expired, _repo.Status(old.SubscriptionId));
    }

    [Fact(DisplayName = "UTC-WB08-09 Buyer without an Approved profile is Forbidden")]
    public async Task Overview_NotApproved()
    {
        _repo.BuyerStatus = "PendingApproval";

        Assert.Equal("Forbidden", (await Service().GetOverviewAsync(default)).Error!.Kind);
    }

    // ---------- Purchase ----------

    [Fact(DisplayName = "UTC-WB08-10 Free tier is activated without a payment request (Step 6)")]
    public async Task Purchase_Free()
    {
        var result = (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Free.PackageId), default)).Value!;

        Assert.Equal("Activated", result.Status);
        Assert.Null(result.Payment);
        Assert.Empty(_repo.Payments);
        var sub = Assert.Single(_repo.Subscriptions);
        Assert.Equal(SubscriptionStatuses.Active, sub.Status);
        Assert.Equal(Now.AddDays(30), sub.EndDate);
        Assert.Contains("Cơ bản", result.Message);
    }

    [Fact(DisplayName = "UTC-WB08-11 Paid tier returns a SePay QR and a Pending SubscriptionPayment (Step 7)")]
    public async Task Purchase_Paid()
    {
        var result = (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId, true), default)).Value!;

        Assert.Equal("PaymentRequired", result.Status);
        Assert.Equal(500_000m, result.Payment!.Amount);
        Assert.StartsWith("SUB", result.Payment.Reference);
        var payment = Assert.Single(_repo.Payments);
        Assert.Equal(SubscriptionPaymentStatuses.Pending, payment.Status);
        Assert.Equal(Now + SubscriptionRules.PaymentWindow, payment.ExpiresAt);
        Assert.Empty(_repo.Subscriptions);
    }

    [Fact(DisplayName = "UTC-WB08-12 A second click reuses the open payment request")]
    public async Task Purchase_ReusesOpenRequest()
    {
        var first = (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId), default)).Value!;
        var second = (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId), default)).Value!;

        Assert.Equal(first.Payment!.Reference, second.Payment!.Reference);
        Assert.Single(_repo.Payments);
    }

    [Fact(DisplayName = "UTC-WB08-13 Inactive or unknown tier is rejected (AF2)")]
    public async Task Purchase_Inactive()
    {
        _repo.Tiers[1] = Silver with { IsActive = false };

        var inactive = await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId), default);
        var unknown = await Service().PurchaseAsync(new PurchaseSubscriptionRequest(99), default);

        Assert.Equal("TIER_UNAVAILABLE", inactive.Error!.Code);
        Assert.Equal("TIER_UNAVAILABLE", unknown.Error!.Code);
        Assert.Empty(_repo.Payments);
    }

    [Fact(DisplayName = "UTC-WB08-14 Selecting the current tier with no renewal due is rejected (AF7)")]
    public async Task Purchase_SameTier()
    {
        Active(Silver, Now.AddDays(-5), Now.AddDays(25));

        var result = await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId), default);

        Assert.Equal("SAME_TIER", result.Error!.Code);
        Assert.Equal(SubscriptionRules.SameTier, result.Error.Message);
        Assert.Empty(_repo.Payments);
    }

    [Fact(DisplayName = "UTC-WB08-15 Downgrade needs confirmation, then is scheduled from EndDate (AF13)")]
    public async Task Purchase_DowngradeScheduled()
    {
        Active(Silver, Now.AddDays(-5), Now.AddDays(25));

        var ask = await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Free.PackageId), default);
        var done = (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Free.PackageId, ConfirmDowngrade: true), default)).Value!;

        Assert.Equal("DOWNGRADE_CONFIRM_REQUIRED", ask.Error!.Code);
        Assert.Equal("Scheduled", done.Status);
        var scheduled = _repo.Subscriptions.Single(s => s.Status == SubscriptionStatuses.Scheduled);
        Assert.Equal(Now.AddDays(25), scheduled.StartDate);
        Assert.Single(_repo.Subscriptions, s => s.Status == SubscriptionStatuses.Active);
    }

    [Fact(DisplayName = "UTC-WB08-16 Payment service unavailable keeps the subscription unchanged (AF12)")]
    public async Task Purchase_PaymentUnavailable()
    {
        _qr.Available = false;

        var result = await Service().PurchaseAsync(new PurchaseSubscriptionRequest(Silver.PackageId), default);

        Assert.Equal("PAYMENT_UNAVAILABLE", result.Error!.Code);
        Assert.Equal(SubscriptionRules.Msg115, result.Error.Message);
        Assert.Empty(_repo.Payments);
    }

    // ---------- Webhook (Step 9-11, AF3-AF6) ----------

    private async Task<string> OpenPayment(TierRecord tier, bool autoRenew = false) =>
        (await Service().PurchaseAsync(new PurchaseSubscriptionRequest(tier.PackageId, autoRenew), default))
            .Value!.Payment!.Reference;

    [Fact(DisplayName = "UTC-WB08-17 Verified payment activates the tier with snapshots and MSG36")]
    public async Task Webhook_Success()
    {
        string reference = await OpenPayment(Gold, autoRenew: true);

        bool handled = await Service(0).TryHandleAsync(Transfer(reference, 5_000_000m), default);

        Assert.True(handled);
        var sub = Assert.Single(_repo.Subscriptions);
        Assert.Equal(SubscriptionStatuses.Active, sub.Status);
        Assert.Equal(Now.AddDays(365), sub.EndDate);
        Assert.True(sub.AutoRenew);
        Assert.True(sub.UnlocksFarmerDirectContact);
        var payment = Assert.Single(_repo.Payments);
        Assert.Equal(SubscriptionPaymentStatuses.Success, payment.Status);
        Assert.Equal(sub.SubscriptionId, payment.SubscriptionId);

        var status = (await Service().GetPaymentAsync(reference, default)).Value!;
        Assert.Equal("Success", status.Status);
        Assert.Contains("Vàng", status.Message);
    }

    [Fact(DisplayName = "UTC-WB08-18 Wrong amount is not a successful payment (AF5, MSG114)")]
    public async Task Webhook_AmountMismatch()
    {
        string reference = await OpenPayment(Silver);

        await Service(0).TryHandleAsync(Transfer(reference, 400_000m), default);

        Assert.Empty(_repo.Subscriptions);
        var status = (await Service().GetPaymentAsync(reference, default)).Value!;
        Assert.Equal(SubscriptionPaymentStatuses.AmountMismatch, status.Status);
        Assert.Equal(SubscriptionRules.Msg114, status.Message);
    }

    [Fact(DisplayName = "UTC-WB08-19 Expired session shows MSG113 and a late transfer does not activate (AF4)")]
    public async Task Webhook_Expired()
    {
        string reference = await OpenPayment(Silver);
        _clock.Advance(TimeSpan.FromMinutes(30));

        var status = (await Service().GetPaymentAsync(reference, default)).Value!;
        await Service(0).TryHandleAsync(Transfer(reference, 500_000m), default);

        Assert.Equal(SubscriptionRules.Msg113, status.Message);
        Assert.Empty(_repo.Subscriptions);
        Assert.Contains(_repo.Payments, p => p.Status == SubscriptionPaymentStatuses.Unmatched);
    }

    [Fact(DisplayName = "UTC-WB08-20 Webhook retry of the same credit is ignored")]
    public async Task Webhook_Duplicate()
    {
        string reference = await OpenPayment(Silver);

        await Service(0).TryHandleAsync(Transfer(reference, 500_000m, id: 7), default);
        bool again = await Service(0).TryHandleAsync(Transfer(reference, 500_000m, id: 7), default);

        Assert.True(again);
        Assert.Single(_repo.Subscriptions);
        Assert.Single(_repo.Payments);
    }

    [Fact(DisplayName = "UTC-WB08-21 Transfer without a SUB reference is left to the order reconciler")]
    public async Task Webhook_NotMine() =>
        Assert.False(await Service(0).TryHandleAsync(
            new IncomingBankTransfer("SePay", "SEPAY-9", 100m, null, null, "{}", ["RC-0011AABBCCDDEEFF"]), default));

    [Fact(DisplayName = "UTC-WB08-22 Paid upgrade replaces the current tier now without proration")]
    public async Task Webhook_Upgrade()
    {
        var old = Active(Silver, Now.AddDays(-5), Now.AddDays(25));
        string reference = await OpenPayment(Gold);

        await Service(0).TryHandleAsync(Transfer(reference, 5_000_000m), default);

        Assert.Equal(SubscriptionStatuses.Replaced, _repo.Status(old.SubscriptionId));
        var active = _repo.Subscriptions.Single(s => s.Status == SubscriptionStatuses.Active);
        Assert.Equal(Gold.PackageId, active.PackageId);
        Assert.Equal(Now, active.StartDate);
    }

    [Fact(DisplayName = "UTC-WB08-23 Paid renewal extends the same row from its EndDate")]
    public async Task Webhook_Renew()
    {
        var old = Active(Silver, Now.AddDays(-25), Now.AddDays(5));
        string reference = await OpenPayment(Silver);

        await Service(0).TryHandleAsync(Transfer(reference, 500_000m), default);

        var sub = Assert.Single(_repo.Subscriptions);
        Assert.Equal(old.SubscriptionId, sub.SubscriptionId);
        Assert.Equal(Now.AddDays(35), sub.EndDate);
    }

    // ---------- AutoRenew, scheduled change, reminders ----------

    [Fact(DisplayName = "UTC-WB08-24 AutoRenew only changes the setting (AF8, AF9)")]
    public async Task AutoRenew()
    {
        var sub = Active(Silver, Now.AddDays(-5), Now.AddDays(25));

        var result = (await Service().SetAutoRenewAsync(new AutoRenewRequest(true), default)).Value!;

        Assert.True(result.AutoRenew);
        Assert.Equal(sub.EndDate, result.EndDate);
        Assert.True(_repo.Subscriptions.Single().AutoRenew);
    }

    [Fact(DisplayName = "UTC-WB08-25 AutoRenew without an active subscription is NotFound")]
    public async Task AutoRenew_NoSubscription() =>
        Assert.Equal("NO_ACTIVE_SUBSCRIPTION",
            (await Service().SetAutoRenewAsync(new AutoRenewRequest(true), default)).Error!.Code);

    [Fact(DisplayName = "UTC-WB08-26 Scheduled downgrade can be cancelled before it starts (AF13)")]
    public async Task CancelScheduled()
    {
        Active(Silver, Now.AddDays(-5), Now.AddDays(25));
        var scheduled = _repo.Seed(Free, SubscriptionStatuses.Scheduled, Now.AddDays(25), Now.AddDays(55));

        var view = (await Service().CancelScheduledAsync(default)).Value!;

        Assert.Null(view.Scheduled);
        Assert.Equal(SubscriptionStatuses.Cancelled, _repo.Status(scheduled.SubscriptionId));
    }

    [Fact(DisplayName = "UTC-WB08-27 Scheduled tier becomes Active when the current one ends")]
    public async Task Rollover_StartsScheduled()
    {
        var old = Active(Silver, Now.AddDays(-30), Now.AddDays(-1));
        var scheduled = _repo.Seed(Free, SubscriptionStatuses.Scheduled, Now.AddDays(-1), Now.AddDays(29));

        var view = (await Service().GetOverviewAsync(default)).Value!;

        Assert.Equal(SubscriptionStatuses.Expired, _repo.Status(old.SubscriptionId));
        Assert.Equal(scheduled.SubscriptionId, view.Current!.SubscriptionId);
        Assert.False(view.IsExpired);
    }

    [Fact(DisplayName = "UTC-WB08-28 Reminders go out 7 and 1 days before EndDate, once each, only with AutoRenew")]
    public async Task Reminders()
    {
        _repo.Candidates.Add(new RenewalCandidate(1, 50, "a@b.vn", "A", "Bạc", Now.AddDays(7)));
        _repo.Candidates.Add(new RenewalCandidate(2, 51, "c@d.vn", "C", "Bạc", Now.AddDays(3)));
        _repo.Candidates.Add(new RenewalCandidate(3, 52, "e@f.vn", "E", "Vàng", Now.AddDays(1)));

        int first = await Service(0).SendRenewalRemindersAsync("http://x/membership", default);
        int second = await Service(0).SendRenewalRemindersAsync("http://x/membership", default);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(new[] { "a@b.vn", "e@f.vn" }, _email.Recipients);
    }
}

internal sealed class FakeSubscriptionRepository : IBuyerSubscriptionRepository
{
    private long _nextId = 100;
    private long _nextPayment = 1;
    private readonly HashSet<string> _events = [];

    public string BuyerStatus { get; set; } = "Approved";
    public List<TierRecord> Tiers { get; } = [];
    public List<SubscriptionRecord> Subscriptions { get; } = [];
    public List<SubscriptionPaymentRecord> Payments { get; } = [];
    public List<RenewalCandidate> Candidates { get; } = [];
    public HashSet<string> RecordedCredits { get; } = [];

    public SubscriptionRecord Seed(TierRecord tier, string status, DateTime start, DateTime end, bool autoRenew = false)
    {
        var record = new SubscriptionRecord(_nextId++, tier.PackageId, tier.PackageName, status, start, end, autoRenew,
            tier.Price, tier.DurationDays, tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate,
            tier.TransactionLimit);
        Subscriptions.Add(record);
        return record;
    }

    public string Status(long id) => Subscriptions.Single(s => s.SubscriptionId == id).Status;

    private BuyerRecord Buyer => new(9, 50, BuyerStatus, "buyer@agri.vn", "Buyer");

    public Task<BuyerRecord?> GetBuyerAsync(long userId, CancellationToken ct) =>
        Task.FromResult<BuyerRecord?>(userId == 50 ? Buyer : null);

    public Task<BuyerRecord?> GetBuyerByProfileAsync(long buyerProfileId, CancellationToken ct) =>
        Task.FromResult<BuyerRecord?>(buyerProfileId == 9 ? Buyer : null);

    public Task<IReadOnlyList<TierRecord>> ListActiveTiersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TierRecord>>(Tiers.Where(t => t.IsActive).ToList());

    public Task<TierRecord?> FindTierAsync(long packageId, CancellationToken ct) =>
        Task.FromResult(Tiers.FirstOrDefault(t => t.PackageId == packageId));

    public Task<IReadOnlyList<SubscriptionRecord>> GetSubscriptionsAsync(long buyerProfileId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SubscriptionRecord>>(Subscriptions
            .Where(s => s.Status is SubscriptionStatuses.Active or SubscriptionStatuses.Scheduled)
            .Concat(Subscriptions.Where(s => s.Status == SubscriptionStatuses.Expired).OrderByDescending(s => s.EndDate).Take(1))
            .ToList());

    public Task SetStatusAsync(long subscriptionId, string status, CancellationToken ct)
    {
        Replace(subscriptionId, s => s with { Status = status });
        return Task.CompletedTask;
    }

    public Task<long> AddAsync(NewSubscription n, CancellationToken ct)
    {
        if (n.Status == SubscriptionStatuses.Active && Subscriptions.Any(s => s.Status == SubscriptionStatuses.Active))
        {
            throw new InvalidOperationException("UX_Subscription_Active violated");
        }
        var record = Seed(n.Tier, n.Status, n.StartDate, n.EndDate, n.AutoRenew);
        return Task.FromResult(record.SubscriptionId);
    }

    public Task ExtendAsync(long subscriptionId, TierRecord tier, DateTime endDate, bool autoRenew, DateTime now,
        CancellationToken ct)
    {
        Replace(subscriptionId, s => s with { EndDate = endDate, AutoRenew = autoRenew, PriceSnapshot = tier.Price });
        return Task.CompletedTask;
    }

    public Task SetAutoRenewAsync(long subscriptionId, bool autoRenew, CancellationToken ct)
    {
        Replace(subscriptionId, s => s with { AutoRenew = autoRenew });
        return Task.CompletedTask;
    }

    public Task<SubscriptionPaymentRecord?> FindOpenPaymentAsync(long buyerProfileId, long packageId, DateTime now,
        CancellationToken ct) =>
        Task.FromResult(Payments.LastOrDefault(p => p.WholesaleBuyerProfileId == buyerProfileId
            && p.PackageId == packageId && p.Status == SubscriptionPaymentStatuses.Pending && p.ExpiresAt > now));

    public Task<SubscriptionPaymentRecord?> FindPaymentAsync(string reference, CancellationToken ct) =>
        Task.FromResult(Payments.LastOrDefault(p => p.Reference == reference));

    public Task AddPaymentAsync(SubscriptionPaymentRecord payment, CancellationToken ct)
    {
        Payments.Add(payment with { PaymentTransactionId = _nextPayment++ });
        return Task.CompletedTask;
    }

    public Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken ct) =>
        Task.FromResult(RecordedCredits.Contains(transactionCode));

    public Task CompletePaymentAsync(long id, string status, IncomingBankTransfer transfer, long? subscriptionId,
        DateTime now, CancellationToken ct)
    {
        int i = Payments.FindIndex(p => p.PaymentTransactionId == id);
        Payments[i] = Payments[i] with { Status = status, SubscriptionId = subscriptionId };
        RecordedCredits.Add(transfer.TransactionCode);
        return Task.CompletedTask;
    }

    public Task AddUnmatchedCreditAsync(IncomingBankTransfer transfer, long? subscriptionId, DateTime now,
        CancellationToken ct)
    {
        Payments.Add(new SubscriptionPaymentRecord(_nextPayment++, "", 0, 0, false, transfer.Amount,
            SubscriptionPaymentStatuses.Unmatched, now, null, subscriptionId));
        RecordedCredits.Add(transfer.TransactionCode);
        return Task.CompletedTask;
    }

    public Task ExpirePaymentAsync(long id, CancellationToken ct)
    {
        int i = Payments.FindIndex(p => p.PaymentTransactionId == id);
        Payments[i] = Payments[i] with { Status = SubscriptionPaymentStatuses.Expired };
        return Task.CompletedTask;
    }

    public Task AddInAppNotificationAsync(long userId, string title, string content, string type, long? subscriptionId,
        string? eventKey, DateTime now, CancellationToken ct)
    {
        if (eventKey != null) _events.Add(eventKey);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RenewalCandidate>> ListRenewalCandidatesAsync(DateTime from, DateTime to,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RenewalCandidate>>(Candidates.Where(c => c.EndDate > from && c.EndDate <= to).ToList());

    public Task<bool> NotificationExistsAsync(string eventKey, CancellationToken ct) =>
        Task.FromResult(_events.Contains(eventKey));

    private void Replace(long id, Func<SubscriptionRecord, SubscriptionRecord> change)
    {
        int i = Subscriptions.FindIndex(s => s.SubscriptionId == id);
        Subscriptions[i] = change(Subscriptions[i]);
    }
}

internal sealed class FakeQr : IPaymentQrProvider
{
    public bool Available { get; set; } = true;

    public PaymentQr? Create(string reference, decimal amount, DateTime expiresAt) => Available
        ? new PaymentQr(reference, amount, expiresAt, "MB Bank", "MB", "0000499456980", "AGRI", $"https://qr/{reference}")
        : null;
}

internal sealed class FakeEmail : INotificationGateway
{
    public List<string> Recipients { get; } = [];

    public Task SendEmailAsync(string recipient, string subject, string content, string eventKey,
        CancellationToken cancellationToken)
    {
        Recipients.Add(recipient);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork : IUseCaseUnitOfWork
{
    public Task<ITransaction> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<ITransaction>(new NoTransaction());

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class NoTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
