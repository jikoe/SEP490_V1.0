using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public class SupplierCapacityRepository(AgriSustainContext database) : ISupplierCapacityRepository
{
    private readonly AgriSustainContext _db = database;

    public async Task<FarmerProfile?> GetFarmerProfileWithDetailsAsync(
        long farmerProfileId,
        CancellationToken cancellationToken)
    {
        return await _db.FarmerProfiles
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FarmCertificates)
            .Include(f => f.CropSeasons)
                .ThenInclude(cs => cs.CultivationLogs)
            .Include(f => f.CropSeasons)
                .ThenInclude(cs => cs.Product)
            .Include(f => f.Products)
                .ThenInclude(p => p.HarvestBatches)
                    .ThenInclude(hb => hb.TraceabilityQrcode)
            .Include(f => f.Products)
                .ThenInclude(p => p.ProductImages)
            .SingleOrDefaultAsync(f => f.FarmerProfileId == farmerProfileId, cancellationToken);
    }

    public async Task<FarmerProfile?> GetFarmerProfileByUserIdAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        return await _db.FarmerProfiles
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FarmCertificates)
            .Include(f => f.CropSeasons)
                .ThenInclude(cs => cs.CultivationLogs)
            .Include(f => f.CropSeasons)
                .ThenInclude(cs => cs.Product)
            .Include(f => f.Products)
                .ThenInclude(p => p.HarvestBatches)
                    .ThenInclude(hb => hb.TraceabilityQrcode)
            .Include(f => f.Products)
                .ThenInclude(p => p.ProductImages)
            .SingleOrDefaultAsync(f => f.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmerProfile>> GetSuppliersDirectoryAsync(
        string? keyword,
        string? province,
        CancellationToken cancellationToken)
    {
        var query = _db.FarmerProfiles
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.FarmCertificates)
            .Include(f => f.CropSeasons)
            .Include(f => f.Products)
            .Where(f => f.VerificationStatus != "Rejected");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmedKeyword = keyword.Trim();
            query = query.Where(f =>
                f.FarmName.Contains(trimmedKeyword) ||
                f.RepresentativeName.Contains(trimmedKeyword) ||
                f.Address.Contains(trimmedKeyword) ||
                (f.ShortDescription != null && f.ShortDescription.Contains(trimmedKeyword)));
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            var trimmedProvince = province.Trim();
            query = query.Where(f =>
                f.ProvinceCode == trimmedProvince ||
                f.Address.Contains(trimmedProvince));
        }

        return await query
            .OrderByDescending(f => f.ReputationScore)
            .ThenBy(f => f.FarmName)
            .ToListAsync(cancellationToken);
    }

    public async Task<FarmCertificate?> GetCertificateDossierAsync(
        long certificateId,
        CancellationToken cancellationToken)
    {
        return await _db.FarmCertificates
            .AsNoTracking()
            .Include(c => c.FarmerProfile)
            .Include(c => c.ReviewedByUser)
            .SingleOrDefaultAsync(c => c.FarmCertificateId == certificateId, cancellationToken);
    }

    public async Task<IReadOnlyList<CultivationLog>> GetSeasonCultivationLogsAsync(
        long cropSeasonId,
        CancellationToken cancellationToken)
    {
        return await _db.CultivationLogs
            .AsNoTracking()
            .Include(l => l.RecordedByUser)
            .Where(l => l.CropSeasonId == cropSeasonId)
            .OrderBy(l => l.ActionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierPerformanceStatsDto> GetFarmerPerformanceStatsAsync(
        long farmerProfileId,
        CancellationToken cancellationToken)
    {
        var totalOrders = await _db.SellerOrders
            .CountAsync(o => o.FarmerProfileId == farmerProfileId, cancellationToken);

        var completedOrders = await _db.SellerOrders
            .CountAsync(o => o.FarmerProfileId == farmerProfileId &&
                (o.Status == "Delivered" || o.Status == "Completed" || o.Status == "Received" || o.Status == "Success"),
                cancellationToken);

        var disputeCount = await _db.DisputeTickets
            .CountAsync(d => d.SellerOrder != null && d.SellerOrder.FarmerProfileId == farmerProfileId, cancellationToken);

        var reviewsQuery = _db.ProductReviews
            .Where(r => r.OrderItem.Product.FarmerProfileId == farmerProfileId && r.ModerationStatus == "Approved");

        var reviewCount = await reviewsQuery.CountAsync(cancellationToken);
        double averageRating = 5.0;

        if (reviewCount > 0)
        {
            averageRating = Math.Round(await reviewsQuery.AverageAsync(r => (double)r.RatingStars, cancellationToken), 1);
        }

        double fulfillmentRate = totalOrders > 0
            ? Math.Round(((double)completedOrders / totalOrders) * 100.0, 1)
            : 100.0;

        double disputeRate = totalOrders > 0
            ? Math.Round(((double)disputeCount / totalOrders) * 100.0, 1)
            : 0.0;

        return new SupplierPerformanceStatsDto(
            TotalOrders: totalOrders,
            CompletedOrders: completedOrders,
            FulfillmentRate: fulfillmentRate,
            DisputeCount: disputeCount,
            DisputeRate: disputeRate,
            AverageRating: averageRating,
            ReviewCount: reviewCount);
    }

    public async Task<IReadOnlyList<SupplierReviewDto>> GetRecentReviewsAsync(
        long farmerProfileId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _db.ProductReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.OrderItem)
                .ThenInclude(oi => oi.Product)
            .Where(r => r.OrderItem.Product.FarmerProfileId == farmerProfileId && r.ModerationStatus == "Approved")
            .OrderByDescending(r => r.ReviewDate)
            .Take(limit)
            .Select(r => new SupplierReviewDto(
                r.ProductReviewId,
                r.User.FullName ?? "Khách hàng mua sỉ",
                r.RatingStars,
                r.Comment,
                r.ReviewDate,
                r.OrderItem.Product.ProductName ?? r.OrderItem.ProductNameSnapshot ?? "Sản phẩm"))
            .ToListAsync(cancellationToken);
    }
}
