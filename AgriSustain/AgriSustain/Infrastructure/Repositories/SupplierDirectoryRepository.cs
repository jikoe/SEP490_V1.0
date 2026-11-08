using AgriSustain.Application.Services.B2B;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-WB-01 read-only queries over FarmerProfile, Product and FarmCertificate.</summary>
public sealed class SupplierDirectoryRepository(AgriSustainContext db) : ISupplierDirectoryRepository
{
    private const string Approved = "Approved";
    private const string ActiveUser = "Active";
    private const string ActiveSubscription = "Active";

    public Task<string?> GetBuyerStatusAsync(long userId, CancellationToken cancellationToken) =>
        db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => (string?)b.VerificationStatus)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasDirectContactAsync(long userId, DateTime now, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.AsNoTracking().AnyAsync(s =>
            s.WholesaleBuyerProfile.UserId == userId
            && s.SubscriptionStatus == ActiveSubscription
            && s.StartDate <= now && s.EndDate > now
            && s.UnlocksFarmerDirectContactSnapshot, cancellationToken);

    public async Task<(IReadOnlyList<SupplierRecord> Items, int Total)> SearchAsync(
        SupplierFilter filter, DateOnly today, CancellationToken cancellationToken)
    {
        // Step 4 / BR-FARM-01: only Approved farms of active accounts are listed.
        var farms = db.FarmerProfiles.AsNoTracking()
            .Where(f => f.VerificationStatus == Approved && f.User.Status == ActiveUser);

        if (filter.Keyword is { } keyword)
        {
            farms = farms.Where(f => f.FarmName.Contains(keyword));
        }

        // Province of the farm address; farms that never filled it fall back to their pickup province.
        if (filter.ProvinceCode is { } province)
        {
            farms = farms.Where(f => f.ProvinceCode == province
                || ((f.ProvinceCode == null || f.ProvinceCode == "") && f.PickupProvinceCode == province));
        }

        // Crop filter counts only active (approved and published) products.
        if (filter.CategoryId is { } categoryId)
        {
            farms = farms.Where(f => f.Products.Any(p => p.ApprovalStatus == Approved && p.IsPublished
                && (p.CategoryId == categoryId || p.Category.ParentCategoryId == categoryId)));
        }

        // DBR-20: only Verified, unexpired certificates count.
        if (filter.CertificateType is { } certificateType)
        {
            // Captured in a local so EF Core can inline the expression into the sub-query.
            var valid = MarketplaceRules.ValidCertificate(today);
            farms = farms.Where(f => f.FarmCertificates.AsQueryable()
                .Where(valid)
                .Any(c => c.CertificateType == certificateType));
        }

        if (filter.ProductionScale is { } scale)
        {
            farms = farms.Where(f => f.ProductionScale == scale);
        }

        int total = await farms.CountAsync(cancellationToken);

        farms = filter.Sort switch
        {
            SupplierDirectoryRules.SortNewest => farms.OrderByDescending(f => f.ApprovedAt)
                .ThenByDescending(f => f.FarmerProfileId),
            SupplierDirectoryRules.SortArea => farms.OrderByDescending(f => f.CultivatedArea ?? 0)
                .ThenByDescending(f => f.ReputationScore).ThenBy(f => f.FarmerProfileId),
            _ => farms.OrderByDescending(f => f.ReputationScore).ThenBy(f => f.FarmName)
                .ThenBy(f => f.FarmerProfileId)
        };

        var page = await farms
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(f => new
            {
                f.FarmerProfileId,
                f.FarmName,
                f.RepresentativeName,
                f.ProvinceCode,
                f.PickupProvinceCode,
                f.ProductionScale,
                f.CultivatedArea,
                f.ReputationScore,
                f.User.PhoneNumber,
                f.User.Email
            })
            .ToListAsync(cancellationToken);

        var ids = page.Select(f => f.FarmerProfileId).ToList();

        var crops = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.FarmerProfileId) && p.ApprovalStatus == Approved && p.IsPublished)
            .Select(p => new { p.FarmerProfileId, p.Category.CategoryName })
            .Distinct()
            .ToListAsync(cancellationToken);

        var certificates = await db.FarmCertificates.AsNoTracking()
            .Where(c => ids.Contains(c.FarmerProfileId))
            .Where(MarketplaceRules.ValidCertificate(today))
            .Select(c => new { c.FarmerProfileId, c.CertificateType })
            .Distinct()
            .ToListAsync(cancellationToken);

        var items = page.Select(f => new SupplierRecord(
            f.FarmerProfileId,
            f.FarmName,
            f.RepresentativeName,
            string.IsNullOrWhiteSpace(f.ProvinceCode) ? f.PickupProvinceCode : f.ProvinceCode,
            f.ProductionScale,
            f.CultivatedArea,
            f.ReputationScore,
            crops.Where(c => c.FarmerProfileId == f.FarmerProfileId).Select(c => c.CategoryName)
                .OrderBy(n => n).ToList(),
            certificates.Where(c => c.FarmerProfileId == f.FarmerProfileId).Select(c => c.CertificateType)
                .OrderBy(n => n).ToList(),
            f.PhoneNumber,
            f.Email)).ToList();

        return (items, total);
    }

    public async Task<IReadOnlyList<NamedOption>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.ParentCategoryId == null)
            .OrderBy(c => c.Priority).ThenBy(c => c.CategoryName)
            .Select(c => new NamedOption(c.CategoryId.ToString(), c.CategoryName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetCertificateTypesAsync(DateOnly today,
        CancellationToken cancellationToken) =>
        await db.FarmCertificates.AsNoTracking()
            .Where(MarketplaceRules.ValidCertificate(today))
            .Select(c => c.CertificateType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetProductionScalesAsync(CancellationToken cancellationToken) =>
        await db.FarmerProfiles.AsNoTracking()
            .Where(f => f.VerificationStatus == Approved && f.ProductionScale != null && f.ProductionScale != "")
            .Select(f => f.ProductionScale!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);
}
