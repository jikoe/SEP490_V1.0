using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class ProductRepository(AgriSustainContext context, TimeProvider clock)
    : IProductRepository
{
    public Task<FarmerProfile?> GetFarmerAsync(long userId, CancellationToken cancellationToken) =>
        context.FarmerProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<ProductOptions> GetOptionsAsync(long userId,
        CancellationToken cancellationToken)
    {
        var farm = await GetFarmerAsync(userId, cancellationToken);
        var categories = await context.Categories.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Priority).ThenBy(x => x.CategoryId)
            .Select(x => new CategoryOption(x.CategoryId, x.CategoryName))
            .ToListAsync(cancellationToken);
        return new ProductOptions(farm?.VerificationStatus == "Approved",
            farm?.VerificationStatus ?? "NotSubmitted", categories);
    }

    public async Task<PageResult<ProductResponse>> SearchAsync(long userId, ProductQuery query,
        CancellationToken cancellationToken)
    {
        var rows = context.Products.AsNoTracking().Where(x => x.FarmerProfile.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            rows = rows.Where(x => x.ProductName.Contains(query.Keyword));
        }

        if (query.CategoryId.HasValue)
        {
            rows = rows.Where(x => x.CategoryId == query.CategoryId);
        }

        if (!string.IsNullOrEmpty(query.ApprovalStatus))
        {
            rows = rows.Where(x => x.ApprovalStatus == query.ApprovalStatus);
        }

        if (query.IsPublished.HasValue)
        {
            rows = rows.Where(x => x.IsPublished == query.IsPublished);
        }

        int total = await rows.CountAsync(cancellationToken);
        var products = await rows.Include(x => x.Category).Include(x => x.ProductImages)
            .Include(x => x.HarvestBatches).Include(x => x.OrderItems)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ProductId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new PageResult<ProductResponse>(products.Select(Map).ToList(),
            total, query.Page, query.PageSize);
    }

    public Task<Product?> GetOwnedAsync(long userId, long id,
        CancellationToken cancellationToken) => context.Products
        .Include(x => x.Category).Include(x => x.ProductImages)
        .Include(x => x.FarmerProfile).Include(x => x.HarvestBatches).Include(x => x.OrderItems)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.ProductId == id
            && x.FarmerProfile.UserId == userId, cancellationToken);

    public Task<bool> IsCategoryActiveAsync(long id, CancellationToken cancellationToken) =>
        context.Categories.AnyAsync(x => x.CategoryId == id && x.IsActive, cancellationToken);

    public Task<bool> IsUnitLockedAsync(long id, CancellationToken cancellationToken) =>
        context.Products.AnyAsync(x => x.ProductId == id
            && (x.HarvestBatches.Any() || x.OrderItems.Any()), cancellationToken);

    public void RemoveImages(Product product)
    {
        context.ProductImages.RemoveRange(product.ProductImages);
        product.ProductImages.Clear();
    }

    public void Add(Product product) => context.Products.Add(product);

    public void Audit(long userId, string action, long? id, string? before, string after) =>
        context.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = userId, EntityType = "Product", EntityId = id,
            Action = action, OldValue = before, NewValue = after,
            OccurredAt = clock.GetUtcNow().UtcDateTime
        });

    public ProductResponse Map(Product product)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().AddHours(7).DateTime);
        var images = product.ProductImages.OrderBy(x => x.DisplayOrder)
            .Select(x => new ProductImageInput(x.ImageUrl, x.IsPrimary, x.AltText)).ToList();
        var fingerprint = JsonSerializer.Serialize(new
        {
            product.ProductName, product.CategoryId, product.Unit, product.OriginProvince,
            product.Description, product.BaseRetailPrice, product.WholesaleMinQuantity,
            product.WholesalePrice, product.ApprovalStatus, product.IsPublished, Images = images
        });
        string version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        return new ProductResponse(product.ProductId, product.ProductName, product.CategoryId,
            product.Category?.CategoryName ?? "", product.Unit, product.OriginProvince,
            product.Description, product.BaseRetailPrice, product.WholesaleMinQuantity,
            product.WholesalePrice, product.ApprovalStatus, product.IsPublished,
            product.RejectionReason, product.HarvestBatches.Where(x => x.IsSaleable(today))
                .Sum(x => x.AvailableQuantity), product.HarvestBatches.Count,
            product.HarvestBatches.Count > 0 || product.OrderItems.Count > 0, version, images);
    }
}


