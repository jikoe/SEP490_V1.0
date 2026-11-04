using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.Marketplace;

/// <summary>UC-MKT-01 unit tests of services, validation and domain rules.</summary>
[Trait("UseCase", "UC-MKT-01")]
public sealed class MarketplaceBrowseServiceTests
{
    private readonly MarketplaceStore _store = new();

    private MarketplaceService Service() => new(_store);

    // ---------- UC-MKT-01: MarketplaceRequestValidator.IsValid ----------

    [Fact(DisplayName = "[UC-MKT-01 #001] Accept the default browse criteria")]
    public void IsValid_DefaultCriteria_AcceptsDefaultCriteria() => Assert.True(MarketplaceRequestValidator.IsValid(new()));

    // ---------- UC-MKT-01: MarketplaceService.BrowseAsync ----------

    [Fact(DisplayName = "[UC-MKT-01 #002] Reject page zero without querying products")]
    public async Task BrowseAsync_PageZero_RejectsWithoutQuery()
    {
        var result = await Service().BrowseAsync(new() { Page = 0 }, default);

        Assert.False(result.Success);
        Assert.Equal(0, _store.BrowseCalls);
    }

    // ---------- UC-MKT-01: MarketplaceRequestValidator.IsValid ----------

    [Fact(DisplayName = "[UC-MKT-01 #003] Reject a page beyond the supported limit")]
    public void IsValid_PageTooLarge_RejectsPage() => Assert.False(MarketplaceRequestValidator.IsValid(new() { Page = 1000001 }));

    [Fact(DisplayName = "[UC-MKT-01 #004] Reject an empty page size")]
    public void IsValid_EmptyPageSize_RejectsPageSize() => Assert.False(MarketplaceRequestValidator.IsValid(new() { PageSize = 0 }));

    [Fact(DisplayName = "[UC-MKT-01 #005] Accept size 100 and reject size 101")]
    public void IsValid_PageSizeLimit_ChecksUpperLimit()
    {
        Assert.True(MarketplaceRequestValidator.IsValid(new() { PageSize = 100 }));
        Assert.False(MarketplaceRequestValidator.IsValid(new() { PageSize = 101 }));
    }

    [Fact(DisplayName = "[UC-MKT-01 #006] Accept 150 characters and reject 151 characters")]
    public void IsValid_KeywordLength_ChecksKeywordLimit()
    {
        Assert.True(MarketplaceRequestValidator.IsValid(new() { Keyword = new('a', 150) }));
        Assert.False(MarketplaceRequestValidator.IsValid(new() { Keyword = new('a', 151) }));
    }

    [Fact(DisplayName = "[UC-MKT-01 #007] Reject control characters in search text")]
    public void IsValid_KeywordControlCharacter_RejectsControlCharacters() => Assert.False(MarketplaceRequestValidator.IsValid(new() { Keyword = "rice\0" }));

    [Fact(DisplayName = "[UC-MKT-01 #008] Accept an inclusive single-price interval")]
    public void IsValid_EqualPriceBounds_AcceptsEqualBounds() => Assert.True(MarketplaceRequestValidator.IsValid(new() { MinPrice = 50000, MaxPrice = 50000 }));

    [Fact(DisplayName = "[UC-MKT-01 #009] Reject inverted price bounds")]
    public void IsValid_InvertedPriceBounds_RejectsInvertedBounds() => Assert.False(MarketplaceRequestValidator.IsValid(new() { MinPrice = 100000, MaxPrice = 50000 }));

    [Fact(DisplayName = "[UC-MKT-01 #010] Reject negative prices")]
    public void IsValid_NegativePrice_RejectsNegativePrice() => Assert.False(MarketplaceRequestValidator.IsValid(new() { MinPrice = -0.01m }));

    [Fact(DisplayName = "[UC-MKT-01 #011] Reject non-finite rating values")]
    public void IsValid_NonFiniteRating_RejectsNonFiniteRating() => Assert.False(MarketplaceRequestValidator.IsValid(new() { MinRating = double.NaN }));

    [Fact(DisplayName = "[UC-MKT-01 #012] Accept ratings 0 and 5, and reject 5.1")]
    public void IsValid_RatingBounds_ChecksRatingLimits()
    {
        Assert.True(MarketplaceRequestValidator.IsValid(new() { MinRating = 0 }));
        Assert.True(MarketplaceRequestValidator.IsValid(new() { MinRating = 5 }));
        Assert.False(MarketplaceRequestValidator.IsValid(new() { MinRating = 5.1 }));
    }

    [Fact(DisplayName = "[UC-MKT-01 #013] Reject blank certification values")]
    public void IsValid_BlankCertification_RejectsBlankValue() => Assert.False(MarketplaceRequestValidator.IsValid(new() { Certifications = [" "] }));

    [Fact(DisplayName = "[UC-MKT-01 #014] Accept 20 values and reject more than 20")]
    public void IsValid_CertificationCount_ChecksCountLimit()
    {
        Assert.True(MarketplaceRequestValidator.IsValid(new() { Certifications = Enumerable.Repeat("VietGAP", 20).ToArray() }));
        Assert.False(MarketplaceRequestValidator.IsValid(new() { Certifications = Enumerable.Repeat("VietGAP", 21).ToArray() }));
    }

    [Fact(DisplayName = "[UC-MKT-01 #015] Reject unknown price-range codes")]
    public void IsValid_UnknownPriceRange_RejectsUnknownRange() => Assert.False(MarketplaceRequestValidator.IsValid(new() { PriceRanges = ["unknown"] }));

    [Fact(DisplayName = "[UC-MKT-01 #016] Reject unsupported sorting")]
    public void IsValid_UnknownSort_RejectsUnknownSort() => Assert.False(MarketplaceRequestValidator.IsValid(new() { Sort = "random" }));

    // ---------- UC-MKT-01: MarketplaceService.BrowseAsync ----------

    [Fact(DisplayName = "[UC-MKT-01 #017] Trim text and preserve the combined criteria sent to the repository")]
    public async Task BrowseAsync_CombinedCriteria_NormalizesAndForwardsCriteria()
    {
        var query = new BrowseMarketplaceRequest
        {
            Keyword = " rice ",
            Region = " An Giang ",
            CategoryId = 1,
            SupplierId = 2,
            CropSeasonId = 3,
            Certifications = ["VietGAP"],
            PriceRanges = ["under50"],
            MinRating = 4,
            Sort = "price-asc",
            Page = 2,
            PageSize = 24
        };
        var result = await Service().BrowseAsync(query, default);

        Assert.True(result.Success);
        Assert.Equal(query with { Keyword = "rice", Region = "An Giang" }, _store.LastQuery);
    }

    [Fact(DisplayName = "[UC-MKT-01 #018] Return a successful empty page and a no-match message")]
    public async Task BrowseAsync_EmptyResults_ReturnsEmptyPage()
    {
        var result = await Service().BrowseAsync(new(), default);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.Total);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    // ---------- UC-MKT-01: MarketplaceService.GetFiltersAsync ----------

    [Fact(DisplayName = "[UC-MKT-01 #019] Return the available filter options")]
    public async Task GetFiltersAsync_FilterOptions_ReturnsAvailableOptions()
    {
        var result = await Service().GetFiltersAsync(default);

        Assert.Equal("Rice", Assert.Single(result.Data!.Categories).Name);
        Assert.Contains("VietGAP", result.Data.Certifications);
        Assert.Contains("An Giang", result.Data.Regions);
    }

    [Fact(DisplayName = "[UC-MKT-01 #020] Reject invalid category identifiers")]
    public async Task GetFiltersAsync_InvalidFilterIdentifier_RejectsInvalidCategory() => Assert.False((await Service().GetFiltersAsync(default, 0)).Success);

    // ---------- UC-MKT-01: MarketplaceRules.PublicProduct ----------

    [Fact(DisplayName = "[UC-MKT-01 #021] Include the product in public visibility")]
    public void PublicProduct_PublicProduct_IncludesEligibleProduct() => Assert.True(MarketplaceRules.PublicProduct.Compile()(MarketplaceStore.PublicProduct()));

    [Fact(DisplayName = "[UC-MKT-01 #022] Exclude an unpublished product")]
    public void PublicProduct_Unpublished_ExcludesUnpublished()
    {
        var product = MarketplaceStore.PublicProduct(); product.IsPublished = false;

        Assert.False(MarketplaceRules.PublicProduct.Compile()(product));
    }

    [Fact(DisplayName = "[UC-MKT-01 #023] Exclude an unapproved product")]
    public void PublicProduct_Unapproved_ExcludesUnapproved()
    {
        var product = MarketplaceStore.PublicProduct(); product.ApprovalStatus = "Pending";

        Assert.False(MarketplaceRules.PublicProduct.Compile()(product));
    }

    [Fact(DisplayName = "[UC-MKT-01 #024] Exclude products from inactive categories")]
    public void PublicProduct_InactiveCategory_ExcludesInactiveCategory()
    {
        var product = MarketplaceStore.PublicProduct(); product.Category.IsActive = false;

        Assert.False(MarketplaceRules.PublicProduct.Compile()(product));
    }

    [Fact(DisplayName = "[UC-MKT-01 #025] Exclude unverified or suspended suppliers")]
    public void PublicProduct_UnverifiedSupplier_ExcludesIneligibleSupplier()
    {
        var product = MarketplaceStore.PublicProduct(); product.FarmerProfile.VerificationStatus = "Pending";

        Assert.False(MarketplaceRules.PublicProduct.Compile()(product));
        product.FarmerProfile.VerificationStatus = "Approved"; product.FarmerProfile.User.Status = "Suspended";

        Assert.False(MarketplaceRules.PublicProduct.Compile()(product));
    }

    // ---------- Test doubles ----------

    private sealed class MarketplaceStore : IMarketplaceRepository
    {
        public BrowseMarketplaceRequest? LastQuery { get; private set; }
        public int BrowseCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public MarketplacePageResponse Page { get; set; } = new([], 0, 1, 12);
        public ProductDetailResponse? Detail { get; set; }
        public Task<MarketplacePageResponse> BrowseAsync(BrowseMarketplaceRequest query, CancellationToken ct)
        {
            BrowseCalls++;
            LastQuery = query;
            return Task.FromResult(Page);
        }
        public Task<MarketplaceFiltersResponse> GetFiltersAsync(CancellationToken ct,
            long? categoryId = null, long? supplierId = null) =>
            Task.FromResult(new MarketplaceFiltersResponse([new(1, "Rice", 2)], [], [], ["An Giang"], ["VietGAP"]));
        public Task<ProductDetailResponse?> GetDetailAsync(long productId, CancellationToken ct)
        {
            DetailCalls++;
            return Task.FromResult(Detail);
        }
        public static Product PublicProduct() => new()
        {
            ProductId = 5,
            ProductName = "Rice",
            Unit = "kg",
            BaseRetailPrice = 38000,
            IsPublished = true,
            ApprovalStatus = "Approved",
            Category = new Category { IsActive = true },
            FarmerProfile = new FarmerProfile { VerificationStatus = "Approved", User = new User { Status = "Active" } }
        };
        public static ProductDetailResponse ProductDetail(decimal stock = 10) => new(
            new ProductSummaryResponse
            {
                Id = 5,
                Name = "Rice",
                Unit = "kg",
                Price = 38000,
                AvailableQuantity = stock,
                Supplier = "Unit Farm",
                Category = "Rice"
            },
            "Rice description", "Unit address", true, null, null, [], [], [], [], []);
    }
}
