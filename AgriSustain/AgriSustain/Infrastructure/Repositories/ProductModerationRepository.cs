using AgriSustain.Application.DTOs.Moderation;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class ProductModerationRepository : IProductModerationRepository
{
    private readonly AgriSustainContext _context;

    public ProductModerationRepository(AgriSustainContext context)
    {
        _context = context;
    }

    public async Task<(int Total, IReadOnlyList<ProductModerationListItemDto> Items)> GetListAsync(
        string status,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var products = _context.Products.AsNoTracking()
            .Where(product => status == "Pending"
                ? product.ApprovalStatus == "Pending"
                : product.ApprovalStatus == "Approved" || product.ApprovalStatus == "Rejected");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            products = products.Where(product =>
                product.ProductName.Contains(keyword)
                || product.FarmerProfile.FarmName.Contains(keyword)
                || product.FarmerProfile.User.FullName.Contains(keyword));
        }

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(product => product.CreatedAt)
            .ThenBy(product => product.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductModerationListItemDto(
                product.ProductId,
                product.ProductName,
                product.ApprovalStatus,
                product.Category.CategoryName,
                product.FarmerProfile.User.FullName,
                product.FarmerProfile.FarmName,
                product.CreatedAt,
                product.ProductImages.OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return (total, items);
    }

    public async Task<ProductModerationDetailDto?> GetDetailAsync(
        long productId,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products.AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductImages)
            .Include(item => item.FarmerProfile)
                .ThenInclude(farmer => farmer.User)
            .Include(item => item.HarvestBatches)
                .ThenInclude(batch => batch.CropSeason)
            .Include(item => item.HarvestBatches)
                .ThenInclude(batch => batch.TraceabilityQrcode)
            .SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var certificates = await _context.FarmCertificates.AsNoTracking()
            .Where(certificate => certificate.FarmerProfileId == product.FarmerProfileId)
            .Select(certificate => new ProductModerationCertificateDto(
                certificate.FarmCertificateId,
                certificate.CertificateType,
                certificate.CertificateNumber,
                certificate.IssuedBy,
                certificate.ExpiryDate,
                certificate.DocumentUrl,
                certificate.VerificationStatus))
            .ToListAsync(cancellationToken);
        var batches = product.HarvestBatches.ToList();

        return new ProductModerationDetailDto(
            product.ProductId,
            product.CreatedAt,
            product.ApprovalStatus,
            product.RejectionReason,
            product.ProductName,
            product.Description,
            product.Category.CategoryName,
            product.ProductImages.OrderBy(image => image.DisplayOrder)
                .Select(image => new ProductModerationImageDto(image.ImageUrl, image.AltText))
                .ToList(),
            product.BaseRetailPrice,
            product.WholesalePrice,
            product.WholesaleMinQuantity,
            product.Unit,
            batches.Sum(batch => batch.AvailableQuantity),
            new ProductModerationFarmerDto(
                product.FarmerProfile.User.FullName,
                product.FarmerProfile.User.Email,
                product.FarmerProfile.User.PhoneNumber),
            new ProductModerationFarmDto(
                product.FarmerProfile.FarmName,
                product.FarmerProfile.Address,
                product.FarmerProfile.ProductionScale,
                product.FarmerProfile.ProvinceCode,
                product.FarmerProfile.VerificationStatus),
            batches.Select(batch => new ProductModerationHarvestDto(
                batch.BatchCode,
                batch.HarvestYield,
                batch.AvailableQuantity,
                batch.EstimatedHarvestDate,
                batch.ActualHarvestDate,
                batch.ExpirationDate,
                batch.QualityNotes,
                batch.CropSeason.SeasonName,
                batch.CropSeason.CropVariety))
                .ToList(),
            batches
                .Where(batch => batch.TraceabilityQrcode is not null)
                .Select(batch => new ProductModerationTraceabilityDto(
                    batch.BatchCode,
                    batch.TraceabilityQrcode!.TargetUrl,
                    batch.TraceabilityQrcode.QrcontentString,
                    batch.TraceabilityQrcode.IsRevoked))
                .ToList(),
            certificates);
    }

    public Task<Product?> GetForModerationAsync(
        long productId,
        CancellationToken cancellationToken)
    {
        return _context.Products
            .Include(product => product.FarmerProfile)
            .SingleOrDefaultAsync(product => product.ProductId == productId, cancellationToken);
    }

    public void AddNotification(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
