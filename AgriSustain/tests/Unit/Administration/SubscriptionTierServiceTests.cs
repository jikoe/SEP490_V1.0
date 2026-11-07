using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Administration;

/// <summary>UC-ADM-09 Manage Subscription Tiers.</summary>
public sealed class SubscriptionTierServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 3, 0, 0, TimeSpan.Zero);

    private readonly AdministrationServiceTests.AdminStore _admins = new();
    private readonly TierStore _store = new();
    private readonly Transactions _work = new();

    public SubscriptionTierServiceTests()
    {
        _admins.Users[1] = new User { UserId = 1, Role = "Administrator", Status = "Active" };
        _admins.Users[2] = new User { UserId = 2, Role = "WholesaleBuyer", Status = "Active" };
    }

    private SubscriptionTierService Service(long actor = 1) =>
        new(_store, _admins, _work, new Actor(actor), new FakeClock(T0));

    private static SubscriptionTierRequest Request(string name = " Gói Doanh nghiệp ", int days = 90,
        decimal price = 1_500_000, int? limit = 50, bool active = true, bool confirmed = false,
        string? version = null) =>
        new(name, "Liên hệ trực tiếp nông trại", days, price, true, false, limit, active, confirmed, version);

    private B2bsubscriptionPackage Existing(bool active = true, bool purchased = false)
    {
        var tier = new B2bsubscriptionPackage { PackageId = 4 };
        tier.Apply("Gói Cơ bản", null, 30, 500_000, false, false, null, active);
        _store.Tiers[4] = tier;
        if (purchased)
        {
            _store.Purchased.Add(4);
        }
        return tier;
    }

    [Fact(DisplayName = "UTC-ADM09-01 A valid tier is created, trimmed and audited together (MSG-ST-01)")]
    public async Task Create_Valid_SavedAndAudited()
    {
        var result = await Service().SaveAsync(null, Request(), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var tier = Assert.Single(_store.Tiers.Values);
        Assert.Equal(("Gói Doanh nghiệp", 90, 1_500_000m, 50), (tier.PackageName, tier.DurationDays, tier.Price, tier.TransactionLimit));
        Assert.Equal("Đã lưu gói Gói Doanh nghiệp.", result.Value!.Message);
        Assert.Equal("CreateTier", Assert.Single(_store.Audits).Action);
        Assert.Equal((1, 1), (_work.Begins, _work.Commits));
    }

    [Theory(DisplayName = "UTC-ADM09-02 Invalid tier details are highlighted per field (BR-ST-01)")]
    [InlineData("", 30, 0, null, "Name")]
    [InlineData("Gói", 0, 0, null, "DurationDays")]
    [InlineData("Gói", 30, -1, null, "Price")]
    [InlineData("Gói", 30, 100.5, null, "Price")]
    [InlineData("Gói", 30, 0, 0, "TransactionLimit")]
    public async Task Create_Invalid_FieldErrors(string name, int days, double price, int? limit, string field)
    {
        var result = await Service().SaveAsync(null, Request(name, days, (decimal)price, limit), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Empty(_store.Tiers);
    }

    [Fact(DisplayName = "UTC-ADM09-03 A name over 100 characters is refused")]
    public async Task Create_LongName_Refused()
    {
        var result = await Service().SaveAsync(null, Request(new string('A', 101)), default);

        Assert.Contains("Name", result.Error!.Fields!.Keys);
    }

    [Fact(DisplayName = "UTC-ADM09-04 A duplicate name shows MSG-ST-02 under the name")]
    public async Task Create_Duplicate_MsgSt02()
    {
        _store.Names.Add("gói doanh nghiệp");

        var result = await Service().SaveAsync(null, Request(), default);

        Assert.Equal("TIER_DUPLICATE", result.Error!.Code);
        Assert.Equal("Đã có gói tên Gói Doanh nghiệp.", result.Error.Fields!["Name"][0]);
    }

    [Fact(DisplayName = "UTC-ADM09-05 Editing with an old version is refused (MSG122)")]
    public async Task Edit_StaleVersion_Changed()
    {
        Existing();

        var result = await Service().SaveAsync(4, Request(version: "OLD"), default);

        Assert.Equal(("TIER_CHANGED", "Conflict"), (result.Error!.Code, result.Error.Kind));
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "UTC-ADM09-06 Editing with the current version updates the tier only (BR-ST-03)")]
    public async Task Edit_CurrentVersion_Updated()
    {
        var tier = Existing();

        var result = await Service().SaveAsync(4, Request(name: "Gói Cơ bản", price: 600_000,
            version: tier.Version()), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(600_000m, tier.Price);
        Assert.Equal("UpdateTier", Assert.Single(_store.Audits).Action);
        Assert.NotNull(_store.Audits[0].OldValue);
    }

    [Fact(DisplayName = "UTC-ADM09-07 Changing the status inside the form needs the MSG-ST-03 confirmation")]
    public async Task Edit_StatusChange_NeedsConfirmation()
    {
        var tier = Existing(active: true);

        var refused = await Service().SaveAsync(4, Request(name: "Gói Cơ bản", active: false,
            version: tier.Version()), default);
        var confirmed = await Service().SaveAsync(4, Request(name: "Gói Cơ bản", active: false, confirmed: true,
            version: tier.Version()), default);

        Assert.Equal("CONFIRMATION_REQUIRED", refused.Error!.Code);
        Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
        Assert.False(tier.IsActive);
    }

    [Fact(DisplayName = "UTC-ADM09-08 Deactivating keeps current subscriptions and is audited (BR-ST-02)")]
    public async Task Deactivate_Audited()
    {
        var tier = Existing(active: true, purchased: true);
        _store.ActiveCount = 3;

        var result = await Service().SetActiveAsync(4, false, new(true, tier.Version()), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(tier.IsActive);
        Assert.Contains("3 thuê bao hiện tại", result.Value!.Message);
        Assert.Equal("DeactivateTier", Assert.Single(_store.Audits).Action);
    }

    [Fact(DisplayName = "UTC-ADM09-09 A tier that has been purchased cannot be deleted (MSG-ST-05, BR-ST-04)")]
    public async Task Delete_Purchased_MsgSt05()
    {
        var tier = Existing(active: false, purchased: true);

        var result = await Service().DeleteAsync(4, new(true, tier.Version()), default);

        Assert.Equal(("TIER_PURCHASED", "Conflict"), (result.Error!.Code, result.Error.Kind));
        Assert.Contains(4L, _store.Tiers.Keys);
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "UTC-ADM09-10 A tier never purchased is deleted and audited")]
    public async Task Delete_NeverPurchased_Removed()
    {
        var tier = Existing();

        var result = await Service().DeleteAsync(4, new(true, tier.Version()), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_store.Tiers);
        Assert.Equal("DeleteTier", Assert.Single(_store.Audits).Action);
    }

    [Fact(DisplayName = "UTC-ADM09-11 Status change or delete without confirmation or version is refused")]
    public async Task Change_WithoutConfirmation_Refused()
    {
        var tier = Existing();

        var notConfirmed = await Service().DeleteAsync(4, new(false, tier.Version()), default);
        var noVersion = await Service().SetActiveAsync(4, false, new(true, null), default);

        Assert.Equal("VALIDATION", notConfirmed.Error!.Code);
        Assert.Contains("Version", noVersion.Error!.Fields!.Keys);
        Assert.True(tier.IsActive);
    }

    [Fact(DisplayName = "UTC-ADM09-12 Only an Active Administrator can manage tiers")]
    public async Task NonAdministrator_Forbidden()
    {
        var result = await Service(actor: 2).SaveAsync(null, Request(), default);

        Assert.Equal("Forbidden", result.Error!.Kind);
        Assert.Empty(_store.Tiers);
    }

    private sealed record Actor(long UserId) : IActorContext;

    private sealed class Transactions : IUseCaseUnitOfWork
    {
        public int Begins { get; private set; }
        public int Commits { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken ct)
        {
            Begins++;
            return Task.FromResult<ITransaction>(new Transaction(this));
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        private sealed class Transaction(Transactions owner) : ITransaction
        {
            public Task CommitAsync(CancellationToken ct)
            {
                owner.Commits++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class TierStore : ISubscriptionTierRepository
    {
        private long _nextId = 10;
        public Dictionary<long, B2bsubscriptionPackage> Tiers { get; } = [];
        public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<long> Purchased { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public int ActiveCount { get; set; }

        public Task<Page<SubscriptionTierDto>> SearchAsync(SubscriptionTierSearch search, DateTime now,
            CancellationToken ct) => Task.FromResult(new Page<SubscriptionTierDto>(
                Tiers.Values.Select(Dto).ToList(), Tiers.Count, search.Page, search.PageSize));

        public Task<SubscriptionTierDto?> GetAsync(long id, DateTime now, CancellationToken ct) =>
            Task.FromResult(Tiers.TryGetValue(id, out var tier) ? Dto(tier) : null);

        public Task<B2bsubscriptionPackage?> FindAsync(long id, CancellationToken ct) =>
            Task.FromResult(Tiers.GetValueOrDefault(id));

        public Task<bool> NameExistsAsync(string name, long exceptId, CancellationToken ct) =>
            Task.FromResult(Names.Contains(name)
                || Tiers.Values.Any(tier => tier.PackageId != exceptId
                    && string.Equals(tier.PackageName, name, StringComparison.OrdinalIgnoreCase)));

        public Task<int> CountActiveSubscriptionsAsync(long id, DateTime now, CancellationToken ct) =>
            Task.FromResult(ActiveCount);

        public Task<bool> HasSubscriptionsAsync(long id, CancellationToken ct) =>
            Task.FromResult(Purchased.Contains(id));

        public void Add(B2bsubscriptionPackage tier)
        {
            tier.PackageId = _nextId++;
            Tiers[tier.PackageId] = tier;
        }

        public void Remove(B2bsubscriptionPackage tier) => Tiers.Remove(tier.PackageId);

        public void AddAudit(AuditLog audit) => Audits.Add(audit);

        private SubscriptionTierDto Dto(B2bsubscriptionPackage tier) => new(tier.PackageId, tier.PackageName,
            tier.PackageDescription, tier.DurationDays, tier.Price, tier.UnlocksFarmerDirectContact,
            tier.CanDownloadContractTemplate, tier.TransactionLimit, tier.IsActive, ActiveCount,
            Purchased.Contains(tier.PackageId), tier.Version());
    }
}
