using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.Administration;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Finance;

/// <summary>UC-FIN-05 Manage Platform Fees.</summary>
public sealed class PlatformFeeServiceTests
{
    // 10:00 on 07/11/2026 in Vietnam.
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 7);
    private const long Finance = 5, Admin = 1, Admin2 = 2;

    private readonly AdministrationServiceTests.AdminStore _users = new();
    private readonly FeeStore _store = new();

    public PlatformFeeServiceTests()
    {
        _users.Users[Finance] = new User { UserId = Finance, Role = "FinanceStaff", Status = "Active" };
        _users.Users[Admin] = new User { UserId = Admin, Role = "Administrator", Status = "Active" };
        _users.Users[Admin2] = new User { UserId = Admin2, Role = "SystemAdmin", Status = "Active" };
        _store.Categories[7] = false;
        _store.Categories[8] = true;   // archived
    }

    private PlatformFeeService Service(long actor = Finance) =>
        new(_store, _users, new Work(_store), new Actor(actor), new FakeClock(T0));

    private static FeePolicyInput Input(decimal rate = 5, bool all = true, long[]? categories = null, int fromDays = 0,
        string method = FeePolicy.MethodPercentage, decimal? fixedAmount = null) =>
        new(method, FeePolicy.BaseItemSubtotal, method == FeePolicy.MethodPercentage ? rate : null, fixedAmount, null, null,
            "Retail", all, categories, Today.AddDays(fromDays), null, "Áp dụng mức mới");

    private static CreateFeeRequest Create(string code = "platform_commission", string group = FeeType.GroupPlatformCommission,
        FeePolicyInput? policy = null, bool submit = true) =>
        new(code, "Phí hoa hồng sàn", null, group, FeeType.RefundProportional, policy ?? Input(), submit);

    private async Task<FeeTypeDto> CreatedAsync(CreateFeeRequest? request = null)
    {
        var result = await Service().CreateAsync(request ?? Create(), default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.Fee!;
    }

    [Fact(DisplayName = "UTC-FIN05-01 A new fee is saved with an upper-case code and its policy waits for approval (MSG193)")]
    public async Task Create_Submitted()
    {
        var fee = await CreatedAsync();

        Assert.Equal(("PLATFORM_COMMISSION", "Draft"), (fee.FeeCode, fee.Status));
        var policy = Assert.Single(fee.Policies);
        Assert.Equal(("PendingApproval", "PLATFORM_COMMISSION-RETAIL-V1"), (policy.State, policy.PolicyCode));
        Assert.Contains(_store.Notifications, n => n.UserId == Admin && n.NotificationType == "PlatformFee");
        Assert.Contains(_store.Audits, a => a.Action == "CreateFeeType");
    }

    [Theory(DisplayName = "UTC-FIN05-02 Invalid fee data is highlighted per field (BR-FEE-01, BR-FEE-02, BR-FEE-04)")]
    [InlineData("AB", 5, 0, "FeeCode")]
    [InlineData("FEE", 0, 0, "Rate")]
    [InlineData("FEE", 101, 0, "Rate")]
    [InlineData("FEE", 5, -1, "EffectiveFrom")]
    public async Task Create_Invalid(string code, decimal rate, int fromDays, string field)
    {
        var result = await Service().CreateAsync(Create(code, policy: Input(rate, fromDays: fromDays)), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Empty(_store.Types);
    }

    [Fact(DisplayName = "UTC-FIN05-03 A duplicate fee code shows MSG195")]
    public async Task Create_DuplicateCode()
    {
        await CreatedAsync();

        var result = await Service().CreateAsync(Create(), default);

        Assert.Equal("Mã phí PLATFORM_COMMISSION đã tồn tại.", result.Error!.Fields!["FeeCode"][0]);
    }

    [Theory(DisplayName = "UTC-FIN05-04 Only commission and service fees may target categories; archived ones are refused (BR-FEE-03, BR-FEE-11)")]
    [InlineData(FeeType.GroupTaxWithholding, 7)]
    [InlineData(FeeType.GroupPlatformCommission, 8)]
    public async Task Create_CategoryScopeRules(string group, long category)
    {
        var result = await Service().CreateAsync(Create("FEE_X", group, Input(all: false, categories: [category])), default);

        Assert.Contains("CategoryIds", result.Error!.Fields!.Keys);
    }

    [Fact(DisplayName = "UTC-FIN05-05 The author cannot approve; Finance cannot approve at all")]
    public async Task Approve_AuthorAndFinanceRefused()
    {
        var fee = await CreatedAsync();
        var policy = fee.Policies[0];

        var byFinance = await Service(Finance).ApprovePolicyAsync(policy.Id, new(policy.Version), default);

        Assert.Equal("Forbidden", byFinance.Error!.Kind);
    }

    [Fact(DisplayName = "UTC-FIN05-06 Approval activates the policy, notifies the author (MSG194) and ends the previous version")]
    public async Task Approve_EndsPreviousVersion()
    {
        var fee = await CreatedAsync();
        var first = fee.Policies[0];
        Assert.True((await Service(Admin).ApprovePolicyAsync(first.Id, new(first.Version), default)).IsSuccess);
        var next = await Service().AddPolicyAsync(fee.Id, new(Input(rate: 7, fromDays: 5)), default);
        var second = next.Value!.Fee!.Policies.First(p => p.State == "PendingApproval");

        var result = await Service(Admin2).ApprovePolicyAsync(second.Id, new(second.Version), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var policies = result.Value!.Fee!.Policies;
        Assert.Equal(Today.AddDays(4), policies.Single(p => p.Id == first.Id).EffectiveTo);
        Assert.Equal("Scheduled", policies.Single(p => p.Id == second.Id).State);
        Assert.Equal("Active", result.Value.Fee.Status);
        Assert.Contains(_store.Notifications, n => n.UserId == Finance && n.Title.Contains("duyệt"));
    }

    [Fact(DisplayName = "UTC-FIN05-07 Two pending policies for the same scope and period overlap (MSG189)")]
    public async Task Overlap_Refused()
    {
        var fee = await CreatedAsync();

        var result = await Service().AddPolicyAsync(fee.Id, new(Input(rate: 6)), default);

        Assert.Equal("FEE_OVERLAP", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-FIN05-08 A category rate needs an approved all-categories rate first (BR-FEE-10)")]
    public async Task CategoryPolicy_NeedsDefault()
    {
        var fee = await CreatedAsync(Create(policy: Input(all: false, categories: [7])));
        var policy = fee.Policies[0];

        var result = await Service(Admin).ApprovePolicyAsync(policy.Id, new(policy.Version), default);

        Assert.Equal("FEE_POLICY_REQUIRED", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-FIN05-09 Reject needs a reason and returns the policy to Draft (MSG191)")]
    public async Task Reject_ReturnsToDraft()
    {
        var fee = await CreatedAsync();
        var policy = fee.Policies[0];

        var noReason = await Service(Admin).RejectPolicyAsync(policy.Id, new(policy.Version), default);
        var rejected = await Service(Admin).RejectPolicyAsync(policy.Id, new(policy.Version, "Tỷ lệ quá cao"), default);

        Assert.Contains("Note", noReason.Error!.Fields!.Keys);
        Assert.Equal(("Draft", "Tỷ lệ quá cao"), (rejected.Value!.Fee!.Policies[0].State, rejected.Value.Fee.Policies[0].ReviewNote));
    }

    [Fact(DisplayName = "UTC-FIN05-10 A mandatory tax cannot be ended without a replacement (MSG192)")]
    public async Task EndDate_MandatoryNeedsReplacement()
    {
        var fee = await CreatedAsync(Create("TAX_VAT", FeeType.GroupTaxWithholding, Input(rate: 1)));
        var policy = fee.Policies[0];
        await Service(Admin).ApprovePolicyAsync(policy.Id, new(policy.Version), default);
        var approved = _store.Types[0].Policies.First();

        var result = await Service().EndDatePolicyAsync(approved.FeePolicyId,
            new(Today.AddDays(10), "Thay đổi luật", PlatformFeeService.VersionOf(approved)), default);

        Assert.Equal("FEE_POLICY_REQUIRED", result.Error!.Code);
        Assert.Null(approved.PendingEffectiveTo);
    }

    [Fact(DisplayName = "UTC-FIN05-11 A fee applied to orders cannot be deleted (MSG190)")]
    public async Task Delete_Applied_Refused()
    {
        var fee = await CreatedAsync(Create(submit: false));
        _store.Applied.Add(fee.Id);

        var result = await Service().DeleteTypeAsync(fee.Id, default);

        Assert.Equal("FEE_APPLIED", result.Error!.Code);
        Assert.Single(_store.Types);
    }

    [Fact(DisplayName = "UTC-FIN05-12 A Draft fee never applied can be deleted")]
    public async Task Delete_Draft()
    {
        var fee = await CreatedAsync(Create(submit: false));

        var result = await Service().DeleteTypeAsync(fee.Id, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(_store.Types);
    }

    [Fact(DisplayName = "UTC-FIN05-13 The preview shows each fee and the Farmer's net on a sample order")]
    public async Task Preview_WithDraft()
    {
        var result = await Service().PreviewAsync(new FeePreviewRequest("Retail", Today,
            [new(7, 100_000)], 20_000, null, FeeType.GroupPlatformCommission, Input(rate: 10)), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal((10_000m, 110_000m), (result.Value!.TotalFees, result.Value.NetAmount));
        Assert.True(Assert.Single(result.Value.Fees).FromDraft);
    }

    private sealed record Actor(long UserId) : IActorContext;

    private sealed class Work(FeeStore store) : IUseCaseUnitOfWork
    {
        public Task<ITransaction> BeginAsync(CancellationToken ct) => Task.FromResult<ITransaction>(new Tx());
        public Task SaveChangesAsync(CancellationToken ct) { store.AssignIds(); return Task.CompletedTask; }
        private sealed class Tx : ITransaction
        {
            public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FeeStore : IFeeRepository
    {
        private long _typeId = 100, _policyId = 500;
        public List<FeeType> Types { get; } = [];
        public Dictionary<long, bool> Categories { get; } = [];
        public HashSet<long> Applied { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<Notification> Notifications { get; } = [];

        public void AssignIds()
        {
            foreach (var type in Types)
            {
                if (type.FeeTypeId == 0) type.FeeTypeId = ++_typeId;
                foreach (var policy in type.Policies)
                {
                    policy.FeeTypeId = type.FeeTypeId;
                    if (policy.FeePolicyId == 0) policy.FeePolicyId = ++_policyId;
                }
            }
        }

        public Task<IReadOnlyList<FeeType>> AllTypesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FeeType>>(Types);
        public Task<FeeType?> FindTypeAsync(long id, CancellationToken ct) => Task.FromResult(Types.FirstOrDefault(t => t.FeeTypeId == id));
        public Task<FeePolicy?> FindPolicyAsync(long id, CancellationToken ct) =>
            Task.FromResult(Types.SelectMany(t => t.Policies).FirstOrDefault(p => p.FeePolicyId == id));
        public Task<FeePolicy?> FindPolicyByRequestKeyAsync(string key, CancellationToken ct) =>
            Task.FromResult(Types.SelectMany(t => t.Policies).FirstOrDefault(p => p.RequestKey == key));
        public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => Task.FromResult(Types.Any(t => t.FeeCode == code));
        public Task<bool> IsAppliedAsync(long id, CancellationToken ct) => Task.FromResult(Applied.Contains(id));
        public Task<IReadOnlyDictionary<long, bool>> CategoriesAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, bool>>(Categories.Where(c => ids.Contains(c.Key)).ToDictionary(c => c.Key, c => c.Value));
        public Task<IReadOnlyList<FeePolicy>> ActivePoliciesAsync(string orderType, DateOnly date, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FeePolicy>>(Types.SelectMany(t => t.Policies).Where(p => p.OrderType == orderType && p.IsActiveOn(date)).ToList());
        public Task<IReadOnlyDictionary<long, long>> ProductCategoriesAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<long, long>>(new Dictionary<long, long>());
        public Task<IReadOnlyList<OrderItemFee>> SellerOrderFeesAsync(long id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrderItemFee>>([]);
        public Task<IReadOnlyList<long>> AdministratorIdsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<long>>([Admin, Admin2]);
        public Task<IReadOnlyList<FeeCategoryOption>> SelectableCategoriesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FeeCategoryOption>>([]);
        public void AddType(FeeType type) => Types.Add(type);
        public void RemoveType(FeeType type) => Types.Remove(type);
        public void RemoveCategories(IEnumerable<FeePolicyCategory> categories) { }
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(Notification notification) => Notifications.Add(notification);
    }
}
