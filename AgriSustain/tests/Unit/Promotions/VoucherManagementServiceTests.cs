using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Promotions;
using AgriSustain.Application.Validators.Promotions;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Promotions;

/// <summary>UC-PS-01 Manage Promotions and Vouchers.</summary>
public sealed class VoucherManagementServiceTests
{
    private const long StaffId = 95;
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly VoucherStore _store = new();

    private VoucherManagementService Service() =>
        new(_store, new Actor(), new SaveVoucherRequestValidator(), new FakeClock(T0));

    private static SaveVoucherRequest Request(string code = " sale10 ", string type = Voucher.TypePercentage,
        decimal value = 10, decimal? max = 50_000, decimal min = 100_000, int? limit = 100,
        int startDays = 0, int endDays = 30, bool active = true) =>
        new(code, type, value, max, min, limit, T0.AddDays(startDays), T0.AddDays(endDays), active);

    private Voucher Existing(int usedCount = 0, int redemptions = 0, int reserved = 0, bool active = true,
        int endDays = 30)
    {
        var voucher = new Voucher
        {
            VoucherId = 4, VoucherCode = "SALE10", DiscountType = Voucher.TypePercentage, DiscountValue = 10,
            MaxDiscountAmount = 50_000, MinOrderAmount = 100_000, UsageLimit = 100, UsedCount = usedCount,
            StartDate = T0.UtcDateTime, EndDate = T0.UtcDateTime.AddDays(endDays), IsActive = active
        };
        _store.Vouchers.Add(voucher);
        _store.Redemptions[4] = redemptions;
        _store.Reserved[4] = reserved;
        return voucher;
    }

    [Fact(DisplayName = "UTC-PS01-01 Valid voucher is created with an upper-case code and audited")]
    public async Task Create_Valid_Stored()
    {
        var result = await Service().CreateAsync(Request(), default);

        Assert.True(result.IsSuccess);
        var voucher = Assert.Single(_store.Vouchers);
        Assert.Equal("SALE10", voucher.VoucherCode);
        Assert.Null(voucher.FarmerProfileId);                         // platform voucher
        Assert.Equal(0, voucher.UsedCount);
        Assert.Equal(Voucher.StatusActive, result.Value!.Voucher!.Status);
        Assert.Equal("CreateVoucher", Assert.Single(_store.Audits).Action);
        Assert.Equal(voucher.VoucherId, _store.Audits[0].EntityId);
    }

    [Theory(DisplayName = "UTC-PS01-02 Invalid discount rule, period or limits are highlighted")]
    [InlineData("AB", Voucher.TypePercentage, 10, 30, null, "voucherCode")]
    [InlineData("MÃ GIẢM", Voucher.TypePercentage, 10, 30, null, "voucherCode")]
    [InlineData("SALE", "Gift", 10, 30, null, "discountType")]
    [InlineData("SALE", Voucher.TypePercentage, 101, 30, null, "discountValue")]
    [InlineData("SALE", Voucher.TypeFixedAmount, 0, 30, null, "discountValue")]
    [InlineData("SALE", Voucher.TypeFixedAmount, 10_000, -1, null, "endDate")]
    [InlineData("SALE", Voucher.TypeFixedAmount, 10_000, 30, 0, "usageLimit")]
    public async Task Create_Invalid_Rejected(string code, string type, decimal value, int endDays,
        int? limit, string field)
    {
        var result = await Service().CreateAsync(
            Request(code, type, value, limit: limit, endDays: endDays), default);

        Assert.Contains(field, result.Error!.Fields!.Keys);
        Assert.Empty(_store.Vouchers);
    }

    [Fact(DisplayName = "UTC-PS01-03 Fixed amount voucher may exceed 100")]
    public async Task Create_FixedAmountAbove100_Allowed()
    {
        var result = await Service().CreateAsync(Request(type: Voucher.TypeFixedAmount, value: 20_000), default);

        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-PS01-04 A period that already ended cannot be created")]
    public async Task Create_PastPeriod_Rejected()
    {
        var result = await Service().CreateAsync(Request(startDays: -10, endDays: -1), default);

        Assert.Contains("endDate", result.Error!.Fields!.Keys);
    }

    [Fact(DisplayName = "UTC-PS01-05 Voucher code is unique regardless of case")]
    public async Task Create_DuplicateCode_Conflict()
    {
        Existing();

        var result = await Service().CreateAsync(Request(code: "Sale10"), default);

        Assert.Equal("VOUCHER_CODE_EXISTS", result.Error!.Code);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact(DisplayName = "UTC-PS01-06 Unused voucher can change its whole rule")]
    public async Task Update_Unused_AllFieldsEditable()
    {
        var voucher = Existing();

        var result = await Service().UpdateAsync(4,
            Request(code: "SALE20", type: Voucher.TypeFixedAmount, value: 20_000, max: null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("SALE20", voucher.VoucherCode);
        Assert.Equal(Voucher.TypeFixedAmount, voucher.DiscountType);
        Assert.Equal("UpdateVoucher", Assert.Single(_store.Audits).Action);
    }

    [Fact(DisplayName = "UTC-PS01-07 Used voucher keeps its terms but dates and limit stay editable")]
    public async Task Update_Used_TermsLocked()
    {
        var voucher = Existing(usedCount: 3);

        var changedRule = await Service().UpdateAsync(4, Request(value: 15), default);
        var extended = await Service().UpdateAsync(4, Request(endDays: 60, limit: 200), default);

        Assert.Equal("VOUCHER_TERMS_LOCKED", changedRule.Error!.Code);
        Assert.True(extended.IsSuccess);
        Assert.Equal(10, voucher.DiscountValue);
        Assert.Equal(200, voucher.UsageLimit);
        Assert.Equal(T0.UtcDateTime.AddDays(60), voucher.EndDate);
    }

    [Fact(DisplayName = "UTC-PS01-08 Usage limit cannot drop below used plus reserved")]
    public async Task Update_LimitBelowUsage_Rejected()
    {
        Existing(usedCount: 3, redemptions: 5, reserved: 2);

        var tooLow = await Service().UpdateAsync(4, Request(limit: 4), default);
        var exact = await Service().UpdateAsync(4, Request(limit: 5), default);

        Assert.Contains("usageLimit", tooLow.Error!.Fields!.Keys);
        Assert.True(exact.IsSuccess);
    }

    [Fact(DisplayName = "UTC-PS01-09 Activate and deactivate change status; expired cannot be activated")]
    public async Task SetActive_Rules()
    {
        var voucher = Existing(active: false);

        var activated = await Service().SetActiveAsync(4, true, default);
        voucher.EndDate = T0.UtcDateTime.AddDays(-1);
        voucher.IsActive = false;
        var expired = await Service().SetActiveAsync(4, true, default);

        Assert.Equal(Voucher.StatusActive, activated.Value!.Voucher!.Status);
        Assert.Equal("VOUCHER_EXPIRED", expired.Error!.Code);
        Assert.False(voucher.IsActive);
    }

    [Fact(DisplayName = "UTC-PS01-10 Unused voucher is deleted; used voucher is archived")]
    public async Task Delete_UnusedRemoved_UsedArchived()
    {
        var unused = Existing();
        var deleted = await Service().DeleteAsync(4, default);
        Assert.True(deleted.IsSuccess);
        Assert.DoesNotContain(unused, _store.Vouchers);

        var used = Existing(redemptions: 1);
        var archived = await Service().DeleteAsync(4, default);

        Assert.True(archived.IsSuccess);
        Assert.Contains(used, _store.Vouchers);
        Assert.False(used.IsActive);
        Assert.Equal("ArchiveVoucher", _store.Audits[^1].Action);
    }

    [Theory(DisplayName = "UTC-PS01-11 Lifecycle status follows flag, dates and usage")]
    [InlineData(true, 0, 30, null, 0, Voucher.StatusActive)]
    [InlineData(false, 0, 30, null, 0, Voucher.StatusInactive)]
    [InlineData(true, 2, 30, null, 0, Voucher.StatusScheduled)]
    [InlineData(true, -30, -1, null, 0, Voucher.StatusExpired)]
    [InlineData(true, 0, 30, 5, 5, Voucher.StatusDepleted)]
    public void LifecycleStatus_Derived(bool active, int startDays, int endDays, int? limit, int used,
        string expected)
    {
        var voucher = new Voucher
        {
            IsActive = active, StartDate = T0.UtcDateTime.AddDays(startDays),
            EndDate = T0.UtcDateTime.AddDays(endDays), UsageLimit = limit, UsedCount = used
        };

        Assert.Equal(expected, voucher.LifecycleStatus(T0.UtcDateTime));
    }

    [Fact(DisplayName = "UTC-PS01-12 Unknown or farm voucher returns 404")]
    public async Task Get_Unknown_NotFound()
    {
        var result = await Service().GetAsync(99, default);

        Assert.Equal(404, result.StatusCode);
    }

    private sealed class Actor : IActorContext
    {
        public long UserId => StaffId;
    }

    private sealed class VoucherStore : IVoucherRepository
    {
        private long _nextId = 50;

        public List<Voucher> Vouchers { get; } = [];
        public Dictionary<long, int> Redemptions { get; } = [];
        public Dictionary<long, int> Reserved { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<(int Total, IReadOnlyList<VoucherDto> Items)> SearchAsync(
            VoucherQuery query, DateTime now, CancellationToken ct) =>
            Task.FromResult<(int, IReadOnlyList<VoucherDto>)>((0, []));
        public Task<Voucher?> GetPlatformVoucherAsync(long id, CancellationToken ct) =>
            Task.FromResult(Vouchers.SingleOrDefault(v => v.VoucherId == id && v.FarmerProfileId == null));
        public Task<bool> CodeExistsAsync(string code, long? exceptId, CancellationToken ct) =>
            Task.FromResult(Vouchers.Any(v => string.Equals(v.VoucherCode, code, StringComparison.OrdinalIgnoreCase)
                && v.VoucherId != exceptId));
        public Task<int> CountRedemptionsAsync(long id, CancellationToken ct) =>
            Task.FromResult(Redemptions.GetValueOrDefault(id));
        public Task<int> CountReservedAsync(long id, CancellationToken ct) =>
            Task.FromResult(Reserved.GetValueOrDefault(id));
        public void Add(Voucher voucher) => Vouchers.Add(voucher);
        public void Remove(Voucher voucher) => Vouchers.Remove(voucher);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            foreach (var voucher in Vouchers.Where(v => v.VoucherId == 0))
            {
                voucher.VoucherId = _nextId++;
            }

            return Task.CompletedTask;
        }
    }
}
