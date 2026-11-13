using AgriSustain.Application.Services.B2B;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.DuIte3;

/// <summary>UC-WB-01 Search Suppliers.</summary>
public sealed class SupplierDirectoryServiceTests
{
    private readonly FakeSupplierRepository _repo = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 8, 3, 0, 0, TimeSpan.Zero));

    private SupplierDirectoryService Service(long userId = 50) =>
        new(_repo, new FakeAddressCatalog(), new SupplierSearchQueryValidator(),
            new FakeCurrentUser(userId, "WholesaleBuyer"), _clock);

    private static SupplierRecord Farm(long id, string name = "Nông trại A") =>
        new(id, name, "Nguyễn Văn A", "100000", "Trung bình", 2.5m, 4.6m,
            ["Rau ăn lá", "Củ quả", "Trái cây", "Gia vị"], ["VietGAP"], "0901234567", "farm@agri.vn");

    [Fact(DisplayName = "UTC-WB01-01 Approved buyer sees matching farms, 12 per page, default sort by reputation")]
    public async Task Search_Default()
    {
        _repo.Rows.Add(Farm(1));
        _repo.Total = 30;

        var result = await Service().SearchAsync(new SupplierSearchQuery(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierDirectoryRules.SortReputation, _repo.LastFilter!.Sort);
        Assert.Equal(12, _repo.LastFilter.PageSize);
        Assert.Equal(1, _repo.LastFilter.Page);
        Assert.Equal(30, result.Value!.Total);
        Assert.Equal("Hà Nội", result.Value.Items[0].ProvinceName);
    }

    [Theory(DisplayName = "UTC-WB01-02 Buyer without an Approved profile cannot open the directory (AF5)")]
    [InlineData(null)]
    [InlineData("PendingApproval")]
    [InlineData("Rejected")]
    [InlineData("AdditionalInfoRequired")]
    public async Task Search_BuyerNotApproved(string? status)
    {
        _repo.BuyerStatus = status;

        var result = await Service().SearchAsync(new SupplierSearchQuery(), default);

        Assert.Equal("BUYER_NOT_APPROVED", result.Error!.Code);
        Assert.Equal("Forbidden", result.Error.Kind);
        Assert.Null(_repo.LastFilter);
    }

    [Fact(DisplayName = "UTC-WB01-03 Keyword longer than 100 characters is rejected and no search runs (AF7)")]
    public async Task Search_KeywordTooLong()
    {
        var result = await Service().SearchAsync(new SupplierSearchQuery(Keyword: new string('a', 101)), default);

        Assert.Equal("KEYWORD_TOO_LONG", result.Error!.Code);
        Assert.Equal(SupplierDirectoryRules.KeywordTooLong, result.Error.Fields!["keyword"][0]);
        Assert.Null(_repo.LastFilter);
    }

    [Fact(DisplayName = "UTC-WB01-04 Keyword of exactly 100 characters is accepted and trimmed")]
    public async Task Search_KeywordBoundary()
    {
        var result = await Service().SearchAsync(new SupplierSearchQuery(Keyword: "  " + new string('a', 100) + " "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, _repo.LastFilter!.Keyword!.Length);
    }

    [Fact(DisplayName = "UTC-WB01-05 Contact details are hidden without the direct-contact benefit (AF6)")]
    public async Task Search_ContactLocked()
    {
        _repo.Rows.Add(Farm(1));
        _repo.Total = 1;

        var page = (await Service().SearchAsync(new SupplierSearchQuery(), default)).Value!;

        Assert.True(page.ContactLocked);
        Assert.Equal(SupplierDirectoryRules.ContactLocked, page.Message);
        Assert.Null(page.Items[0].Phone);
        Assert.Null(page.Items[0].Email);
        Assert.True(page.Items[0].ContactLocked);
    }

    [Fact(DisplayName = "UTC-WB01-06 Direct-contact subscription shows phone and e-mail")]
    public async Task Search_ContactUnlocked()
    {
        _repo.Rows.Add(Farm(1));
        _repo.Total = 1;
        _repo.DirectContact = true;

        var page = (await Service().SearchAsync(new SupplierSearchQuery(), default)).Value!;

        Assert.False(page.ContactLocked);
        Assert.Null(page.Message);
        Assert.Equal("0901234567", page.Items[0].Phone);
        Assert.Equal("farm@agri.vn", page.Items[0].Email);
    }

    [Fact(DisplayName = "UTC-WB01-07 No matching farm returns the empty-state message (AF1)")]
    public async Task Search_NoMatch()
    {
        var page = (await Service().SearchAsync(new SupplierSearchQuery(Keyword: "zzz"), default)).Value!;

        Assert.Empty(page.Items);
        Assert.Equal(SupplierDirectoryRules.NoMatch, page.Message);
    }

    [Theory(DisplayName = "UTC-WB01-08 Sort is normalized; unknown values fall back to reputation (AF3)")]
    [InlineData("newest", SupplierDirectoryRules.SortNewest)]
    [InlineData("AREA", SupplierDirectoryRules.SortArea)]
    [InlineData("price", SupplierDirectoryRules.SortReputation)]
    [InlineData(null, SupplierDirectoryRules.SortReputation)]
    public async Task Search_Sort(string? sort, string expected)
    {
        await Service().SearchAsync(new SupplierSearchQuery(Sort: sort), default);

        Assert.Equal(expected, _repo.LastFilter!.Sort);
    }

    [Fact(DisplayName = "UTC-WB01-09 Filters are trimmed, blanks ignored, page below 1 becomes 1 (AF2, AF4)")]
    public async Task Search_FilterNormalization()
    {
        await Service().SearchAsync(new SupplierSearchQuery(" ", " 100000 ", 0, " VietGAP ", "", null, -3), default);

        var f = _repo.LastFilter!;
        Assert.Null(f.Keyword);
        Assert.Equal("100000", f.ProvinceCode);
        Assert.Null(f.CategoryId);
        Assert.Equal("VietGAP", f.CertificateType);
        Assert.Null(f.ProductionScale);
        Assert.Equal(1, f.Page);
    }

    [Fact(DisplayName = "UTC-WB01-10 A card shows at most 3 main crops")]
    public async Task Search_MainCrops()
    {
        _repo.Rows.Add(Farm(1));
        _repo.Total = 1;

        var card = (await Service().SearchAsync(new SupplierSearchQuery(), default)).Value!.Items[0];

        Assert.Equal(3, card.MainCrops.Count);
    }

    [Fact(DisplayName = "UTC-WB01-11 Signed-out caller is Unauthorized")]
    public async Task Search_SignedOut()
    {
        var result = await Service(userId: 0).SearchAsync(new SupplierSearchQuery(), default);

        Assert.Equal("Unauthorized", result.Error!.Kind);
    }

    [Fact(DisplayName = "UTC-WB01-12 Filter panel lists provinces, categories, certificates, scales and 3 sorts")]
    public async Task Filters()
    {
        _repo.CertificateTypes.Add("VietGAP");
        _repo.Scales.Add("Lớn");

        var options = (await Service().GetFilterOptionsAsync(default)).Value!;

        Assert.Single(options.Provinces);
        Assert.Equal(new[] { "VietGAP" }, options.CertificateTypes);
        Assert.Equal(new[] { "Lớn" }, options.ProductionScales);
        Assert.Equal(3, options.Sorts.Count);
    }
}

internal sealed class FakeSupplierRepository : ISupplierDirectoryRepository
{
    public string? BuyerStatus { get; set; } = "Approved";
    public bool DirectContact { get; set; }
    public List<SupplierRecord> Rows { get; } = [];
    public int Total { get; set; }
    public SupplierFilter? LastFilter { get; private set; }
    public List<string> CertificateTypes { get; } = [];
    public List<string> Scales { get; } = [];

    public Task<string?> GetBuyerStatusAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(BuyerStatus);

    public Task<bool> HasDirectContactAsync(long userId, DateTime now, CancellationToken cancellationToken) =>
        Task.FromResult(DirectContact);

    public Task<(IReadOnlyList<SupplierRecord> Items, int Total)> SearchAsync(
        SupplierFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        return Task.FromResult<(IReadOnlyList<SupplierRecord>, int)>((Rows, Total));
    }

    public Task<IReadOnlyList<NamedOption>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<NamedOption>>([new("1", "Rau củ")]);

    public Task<IReadOnlyList<string>> GetCertificateTypesAsync(DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(CertificateTypes);

    public Task<IReadOnlyList<string>> GetProductionScalesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Scales);
}
