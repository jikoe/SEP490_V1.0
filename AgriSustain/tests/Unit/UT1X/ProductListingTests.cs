using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using Xunit;
namespace AgriSustain.UnitTests.UT1X;
[Trait("UseCase", "UC-FAR-03")]
public sealed class ProductListingTests
{
    [Fact]

    [Trait("CaseId", "UT-FAR-03-R2-001")]
    public async Task Submit_ValidOwnedImages_CreatesPendingUnpublishedListing()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Pending", result.Value!.ApprovalStatus); Assert.False(result.Value.IsPublished);
        Assert.Equal(1, f.Products.Adds); Assert.Equal(1, f.Products.Audits); Assert.Equal(1, f.Work.Commits);
        Assert.Equal(0, f.Files.ImageSaves); Assert.Equal(20, f.Batch.AvailableQuantity);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-002")]
    public async Task Submit_EqualWholesalePriceAndMinimumTwo_IsAccepted()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { WholesaleMinQuantity = 2, WholesalePrice = 30000 }, default);
        Assert.True(result.IsSuccess); Assert.Equal(30000, result.Value!.WholesalePrice); Assert.Equal(2, result.Value.WholesaleMinQuantity);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-003")]
    public async Task Submit_UnapprovedProfile_DoesNotPersist()
    {
        var f = new CommerceFixture();
        f.Farmer.VerificationStatus = "Pending";
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default), f, "FORBIDDEN");
        Assert.Equal(0, f.Products.Adds); Assert.Equal(0, f.Files.ImageSaves);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-004")]
    public async Task Submit_MissingProductName_PreservesState()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { ProductName = "" }, default), f);
        Assert.Equal(0, f.Products.Adds);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-005")]
    public async Task Submit_MissingCategory_PreservesState()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { CategoryId = 0 }, default), f);
        Assert.Equal(0, f.Products.Adds);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-006")]
    public async Task Submit_MissingUnit_PreservesState()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { Unit = "" }, default), f);
        Assert.Equal(0, f.Products.Adds);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-008")]
    public async Task Submit_MissingDescription_PreservesState()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { Description = "" }, default), f);
        Assert.Equal(0, f.Products.Adds);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-007")]
    public async Task Submit_DtoHasNoOriginField_IsAccepted()
    {
        var f = new CommerceFixture();
        Assert.Null(typeof(ProductRequest).GetProperty("Origin")); Assert.Null(typeof(ProductRequest).GetProperty("OriginProvince"));
        Assert.True((await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default)).IsSuccess);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-009")]
    public async Task Submit_ZeroRetail_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { BaseRetailPrice = 0 }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-010")]
    public async Task Submit_NegativeRetail_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { BaseRetailPrice = -1 }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-011")]
    public async Task Submit_WholesaleMinimumOne_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { WholesaleMinQuantity = 1 }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-012")]
    public async Task Submit_ZeroWholesale_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { WholesalePrice = 0 }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-013")]
    public async Task Submit_WholesaleExceedsRetail_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { WholesalePrice = 30001 }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-014")]
    public async Task Submit_HalfWholesalePair_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { WholesalePrice = null }, default), f);
        Assert.Equal(30000, f.Product.BaseRetailPrice); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-015")]
    public async Task Submit_MissingPrimaryImage_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { Images = [new ProductImageInput(CommerceFixture.ImageUrl, false, null)] }, default), f);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-016")]
    public async Task Submit_UnownedImageReference_IsRejected()
    {
        var f = new CommerceFixture();
        f.Files.AllowImages = false;
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default), f);
        Assert.Equal(0, f.Files.ImageSaves);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-017")]
    public async Task Submit_OwnedPngReference_DoesNotReuploadBytes()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { Images = [new ProductImageInput("/media/101/photo.png", true, null)] }, default);
        Assert.True(result.IsSuccess); Assert.Equal(0, f.Files.ImageSaves); Assert.Equal(1, f.Work.Commits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-018")]
    public async Task Submit_PathTraversalReference_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { Images = [new ProductImageInput("/media/101/../photo.jpg", true, null)] }, default), f);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-019")]
    public async Task Resubmit_RejectedListing_ReusesIdentityAndReason()
    {
        var f = new CommerceFixture();
        f.Product.ApprovalStatus = "Rejected"; f.Product.RejectionReason = "Old moderation reason";
        var result = await f.FarmerProductService.SaveProductAsync(101, 301, f.Request, default);
        Assert.True(result.IsSuccess); Assert.Equal(301, result.Value!.ProductId);
        Assert.Equal("Pending", result.Value.ApprovalStatus); Assert.False(result.Value.IsPublished);
        Assert.Equal("Old moderation reason", f.Product.RejectionReason); Assert.Equal(0, f.Products.Adds);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-020")]
    public async Task Submit_CommitFailure_DoesNotReturnSuccess()
    {
        var f = new CommerceFixture();
        f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.FarmerProductService.SaveProductAsync(101, null, f.Request, default));
        Assert.Equal(0, f.Work.Commits); Assert.Equal(1, f.Work.Attempts);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-028")]
    public async Task Update_CommitFailure_DoesNotReturnSuccess()
    {
        var f = new CommerceFixture();
        f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { ProductName = "Edited tomato" }, default));
        Assert.Equal(0, f.Work.Commits); Assert.Equal(1, f.Work.Attempts);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-021")]
    public async Task Update_ContentChange_RequiresModeration()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { ProductName = "Edited tomato" }, default);
        Assert.True(result.IsSuccess); Assert.Equal("Pending", result.Value!.ApprovalStatus); Assert.False(result.Value.IsPublished); Assert.Equal(1, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-022")]
    public async Task Update_PriceOnly_PreservesApprovalAndOrderSnapshot()
    {
        var f = new CommerceFixture();
        var old = f.Line.UnitPriceSnapshot;
        var result = await f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { BaseRetailPrice = 32000 }, default);
        Assert.True(result.IsSuccess); Assert.Equal("Approved", result.Value!.ApprovalStatus); Assert.True(result.Value.IsPublished);
        Assert.Equal(old, f.Line.UnitPriceSnapshot); Assert.Equal(0, f.Products.RemovedImages);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-023")]
    public async Task Update_ReferencedUnit_IsLocked()
    {
        var f = new CommerceFixture();
        f.Products.UnitLocked = true;
        Rejected(await f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { Unit = "túi" }, default), f, "CONFLICT");
        Assert.Equal("kg", f.Product.Unit);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-024")]
    public async Task Update_UnreferencedUnit_CanChange()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { Unit = "túi" }, default);
        Assert.True(result.IsSuccess); Assert.Equal("túi", result.Value!.Unit); Assert.Equal("Pending", result.Value.ApprovalStatus);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-025")]
    public async Task Update_ForeignId_IsDenied()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, 999, f.Request, default), f, "NOT_FOUND");
        Assert.Equal("Tomato", f.Product.ProductName);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-027")]
    public async Task Update_NonOwnedListing_IsDenied()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, 999, f.Request, default), f, "NOT_FOUND");
        Assert.Equal("Tomato", f.Product.ProductName);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-026")]
    public async Task Update_InvalidPricePair_PreservesApprovedValues()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, 301, f.Request with { WholesalePrice = 30001 }, default), f);
        Assert.True(f.Product.IsPublished); Assert.Equal("Approved", f.Product.ApprovalStatus); Assert.Equal(25000, f.Product.WholesalePrice);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-029")]
    public async Task Hide_PreservesApprovalAndHistoricalReferences()
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SetVisibilityAsync(101, 301, false, ProductDouble.Version, default);
        Assert.True(result.IsSuccess); Assert.False(result.Value!.IsPublished); Assert.Equal("Approved", result.Value.ApprovalStatus);
        Assert.Equal(301, f.Line.ProductId); Assert.Equal(401, f.Allocation.HarvestBatchId);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-030")]
    public async Task Publish_EligibleApprovedListing_Succeeds()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false;
        Assert.True((await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default)).IsSuccess);
        Assert.True(f.Product.IsPublished); Assert.Equal(1, f.Work.Commits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-031")]
    public async Task Publish_Pending_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Product.ApprovalStatus = "Pending";
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f, "CONFLICT");
        Assert.False(f.Product.IsPublished);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-032")]
    public async Task Publish_Rejected_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Product.ApprovalStatus = "Rejected";
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f, "CONFLICT");
        Assert.False(f.Product.IsPublished);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-033")]
    public async Task Publish_UnapprovedProfile_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Farmer.VerificationStatus = "Rejected";
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f, "FORBIDDEN");
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-034")]
    public async Task Publish_InactiveCategory_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Products.CategoryActive = false;
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f);
        Assert.False(f.Product.IsPublished); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-035")]
    public async Task Publish_InvalidPrice_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Product.BaseRetailPrice = 0;
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f);
        Assert.False(f.Product.IsPublished); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-036")]
    public async Task Publish_MissingPrimary_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false; f.Product.ProductImages.Clear();
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f);
        Assert.False(f.Product.IsPublished); Assert.Equal(0, f.Products.Audits);
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-037")]
    public async Task Visibility_ForeignId_IsDenied()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 999, false, ProductDouble.Version, default), f, "NOT_FOUND");
    }

    [Fact]
    [Trait("CaseId", "UT-FAR-03-R2-038")]
    public async Task Visibility_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.FarmerProductService.SetVisibilityAsync(101, 301, false, ProductDouble.Version, default));
        Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-001")]
    public async Task Submit_MissingCertificate_IsRejectedWithCertRequired()
    {
        var f = new CommerceFixture();
        var res = await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { FarmCertificateId = null }, default);
        Rejected(res, f);
        Assert.True(res.Error!.Fields!.ContainsKey(nameof(ProductRequest.FarmCertificateId)));
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-002")]
    public async Task Submit_ForeignOrUnknownCertificate_IsRejected()
    {
        var f = new CommerceFixture();
        f.Products.Certificates[802] = new Domain.Entities.FarmCertificate
        {
            FarmCertificateId = 802, FarmerProfileId = 999, VerificationStatus = "Verified",
            IssueDate = new DateOnly(2026, 10, 1), ExpiryDate = new DateOnly(2027, 10, 1)
        };
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { FarmCertificateId = 802 }, default), f);
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request with { FarmCertificateId = 9999 }, default), f);
    }

    [Theory, Trait("CaseId", "UT-FAR-03-R3-003")]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    [InlineData("Revoked")]
    public async Task Submit_UnapprovedOrRevokedCertificate_IsRejected(string status)
    {
        var f = new CommerceFixture();
        f.Certificate.VerificationStatus = status;
        Rejected(await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default), f);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-004")]
    public async Task Submit_CertificateDateBoundaries_TodayAcceptedFutureIssueOrPastExpiryRejected()
    {
        var f = new CommerceFixture();
        var today = DateOnly.FromDateTime(f.Clock.Now.AddHours(7).DateTime);
        f.Certificate.IssueDate = today;
        f.Certificate.ExpiryDate = today;
        Assert.True((await f.FarmerProductService.SaveProductAsync(101, null, f.Request, default)).IsSuccess);

        var f2 = new CommerceFixture();
        f2.Certificate.IssueDate = today.AddDays(1);
        Rejected(await f2.FarmerProductService.SaveProductAsync(101, null, f2.Request, default), f2);

        var f3 = new CommerceFixture();
        f3.Certificate.ExpiryDate = today.AddDays(-1);
        Rejected(await f3.FarmerProductService.SaveProductAsync(101, null, f3.Request, default), f3);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-005")]
    public async Task Update_PriceOnlyOnLegacyListingWithoutCertificate_PreservesApproval()
    {
        var f = new CommerceFixture();
        f.Product.FarmCertificateId = null;
        f.Product.FarmCertificate = null;
        var res = await f.FarmerProductService.SaveProductAsync(101, 301,
            f.Request with { FarmCertificateId = null, BaseRetailPrice = 31000, PriceOnly = true }, default);
        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.Equal("Approved", res.Value!.ApprovalStatus);
        Assert.True(res.Value.IsPublished);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-006")]
    public async Task Update_PriceOnlyWithContentChange_IsRejected()
    {
        var f = new CommerceFixture();
        Rejected(await f.FarmerProductService.SaveProductAsync(101, 301,
            f.Request with { ProductName = "Changed Name", PriceOnly = true }, default), f);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-007")]
    public async Task Publish_ApprovedListingWithExpiredCertificate_IsBlocked()
    {
        var f = new CommerceFixture();
        f.Product.IsPublished = false;
        f.Certificate.ExpiryDate = DateOnly.FromDateTime(f.Clock.Now.AddHours(7).DateTime).AddDays(-1);
        Rejected(await f.FarmerProductService.SetVisibilityAsync(101, 301, true, ProductDouble.Version, default), f);
        Assert.False(f.Product.IsPublished);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-008")]
    public async Task Submit_NameAndImageCountBoundaries_EnforcedCorrectly()
    {
        var f1 = new CommerceFixture();
        Assert.True((await f1.FarmerProductService.SaveProductAsync(101, null, f1.Request with { ProductName = new string('A', 255) }, default)).IsSuccess);
        var f2 = new CommerceFixture();
        Rejected(await f2.FarmerProductService.SaveProductAsync(101, null, f2.Request with { ProductName = new string('A', 256) }, default), f2);

        var tenImages = Enumerable.Range(1, 10).Select(i => new ProductImageInput($"/media/101/img{i}.jpg", i == 1, null)).ToList();
        var f3 = new CommerceFixture();
        Assert.True((await f3.FarmerProductService.SaveProductAsync(101, null, f3.Request with { Images = tenImages }, default)).IsSuccess);

        var elevenImages = Enumerable.Range(1, 11).Select(i => new ProductImageInput($"/media/101/img{i}.jpg", i == 1, null)).ToList();
        var f4 = new CommerceFixture();
        Rejected(await f4.FarmerProductService.SaveProductAsync(101, null, f4.Request with { Images = elevenImages }, default), f4);

        var twoPrimaries = new[] { new ProductImageInput("/media/101/a.jpg", true, null), new ProductImageInput("/media/101/b.jpg", true, null) };
        var f5 = new CommerceFixture();
        Rejected(await f5.FarmerProductService.SaveProductAsync(101, null, f5.Request with { Images = twoPrimaries }, default), f5);
    }

    [Fact, Trait("CaseId", "UT-FAR-03-R3-009")]
    public async Task Search_ValidAndInvalidQuery_HandledCorrectly()
    {
        var f = new CommerceFixture();
        var ok = await f.FarmerProductService.GetProductsAsync(101, new ProductQuery { Page = 1, PageSize = 10 }, default);
        Assert.True(ok.IsSuccess);
        Assert.Single(ok.Value!.Items);

        var bad = await f.FarmerProductService.GetProductsAsync(101, new ProductQuery { Page = 0, PageSize = 10 }, default);
        Assert.False(bad.IsSuccess);
        Assert.Equal("VALIDATION", bad.Error!.Code);
    }

    private static void Rejected(FeatureResult<ProductResponse> result, CommerceFixture f, string code = "VALIDATION")
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(0, f.Work.Commits);
        Assert.DoesNotContain("(MSG", result.Error.Message);
    }
}

