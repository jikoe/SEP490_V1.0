using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static AgriSustain.Application.DTOs.Marketplace.MarketplaceFiltersResponse;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class MarketplaceRepository(AgriSustainContext context) : IMarketplaceRepository
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    public async Task<MarketplacePageResponse> BrowseAsync(
        BrowseMarketplaceRequest request, CancellationToken cancellationToken)
    {
        var products = PublicProducts();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            products = products.Where(product => product.ProductName.Contains(request.Keyword)
                || product.Category.CategoryName.Contains(request.Keyword)
                || product.FarmerProfile.FarmName.Contains(request.Keyword));
        }
        if (request.CategoryId.HasValue)
        {
            products = products.Where(product => product.CategoryId == request.CategoryId);
        }
        if (request.SupplierId.HasValue)
        {
            products = products.Where(product => product.FarmerProfileId == request.SupplierId);
        }
        if (request.CropSeasonId.HasValue)
        {
            products = products.Where(product => product.CropSeasons
                .Any(season => season.CropSeasonId == request.CropSeasonId));
        }
        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            products = products.Where(product => product.OriginProvince == request.Region);
        }
        if (request.Certifications.Length > 0)
        {
            var certificates = context.FarmCertificates
                .Where(MarketplaceRules.ValidCertificate(Today));
            products = products.Where(product => certificates.Any(certificate =>
                certificate.FarmerProfileId == product.FarmerProfileId
                && request.Certifications.Contains(certificate.CertificateType)));
        }
        if (request.MinPrice.HasValue)
        {
            products = products.Where(product => product.BaseRetailPrice >= request.MinPrice);
        }
        if (request.MaxPrice.HasValue)
        {
            products = products.Where(product => product.BaseRetailPrice <= request.MaxPrice);
        }
        if (request.PriceRanges.Length > 0)
        {
            products = products.Where(product =>
                (request.PriceRanges.Contains("under50") && product.BaseRetailPrice < 50000)
                || (request.PriceRanges.Contains("50to100")
                    && product.BaseRetailPrice >= 50000 && product.BaseRetailPrice < 100000)
                || (request.PriceRanges.Contains("100to200")
                    && product.BaseRetailPrice >= 100000 && product.BaseRetailPrice <= 200000)
                || (request.PriceRanges.Contains("over200") && product.BaseRetailPrice > 200000));
        }

        var summaries = Summaries(products);
        if (request.MinRating.HasValue)
        {
            summaries = summaries.Where(product => product.Rating >= request.MinRating);
        }
        var total = await summaries.CountAsync(cancellationToken);
        var page = Math.Min(request.Page,
            Math.Max(1, (int)Math.Ceiling((double)total / request.PageSize)));
        var ordered = request.Sort switch
        {
            "price-asc" => summaries.OrderBy(product => product.Price)
                .ThenBy(product => product.Id),
            "price-desc" => summaries.OrderByDescending(product => product.Price)
                .ThenBy(product => product.Id),
            "rating" => summaries.OrderByDescending(product => product.Rating)
                .ThenByDescending(product => product.Id),
            _ => summaries.OrderByDescending(product => context.Products
                    .Where(item => item.ProductId == product.Id)
                    .Select(item => item.CreatedAt).First())
                .ThenByDescending(product => product.Id)
        };
        var items = await ordered.Skip((page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);
        return new MarketplacePageResponse(items, total, page, request.PageSize);
    }

    public async Task<MarketplaceFiltersResponse> GetFiltersAsync(
        CancellationToken cancellationToken)
    {
        var products = PublicProducts();
        var categories = await products.GroupBy(product => new
            {
                product.CategoryId, product.Category.CategoryName, product.Category.Priority
            })
            .OrderBy(group => group.Key.Priority).ThenBy(group => group.Key.CategoryId)
            .Select(group => new FilterOption(
                group.Key.CategoryId, group.Key.CategoryName, group.Count()))
            .ToListAsync(cancellationToken);
        var suppliers = await products.GroupBy(product => new
            {
                product.FarmerProfileId, product.FarmerProfile.FarmName
            })
            .OrderBy(group => group.Key.FarmName)
            .Select(group => new FilterOption(
                group.Key.FarmerProfileId, group.Key.FarmName, group.Count()))
            .ToListAsync(cancellationToken);
        var seasons = await context.CropSeasons.AsNoTracking()
            .Where(season => products.Any(product => product.ProductId == season.ProductId))
            .OrderByDescending(season => season.PlantingDate)
            .Select(season => new FilterOption(season.CropSeasonId, season.SeasonName, 1))
            .ToListAsync(cancellationToken);
        var regions = await products.Where(product => product.OriginProvince != null)
            .Select(product => product.OriginProvince!).Distinct().OrderBy(region => region)
            .ToListAsync(cancellationToken);
        var certifications = await context.FarmCertificates.AsNoTracking()
            .Where(MarketplaceRules.ValidCertificate(Today))
            .Where(certificate => products.Any(product =>
                product.FarmerProfileId == certificate.FarmerProfileId))
            .Select(certificate => certificate.CertificateType).Distinct().OrderBy(type => type)
            .ToListAsync(cancellationToken);
        return new MarketplaceFiltersResponse(
            categories, suppliers, seasons, regions, certifications);
    }

    public async Task<ProductDetailResponse?> GetDetailAsync(
        long productId, CancellationToken cancellationToken)
    {
        var visible = PublicProducts().Where(product => product.ProductId == productId);
        var product = await Summaries(visible).SingleOrDefaultAsync(cancellationToken);
        if (product == null)
        {
            return null;
        }
        var information = await visible.Select(item => new
        {
            item.Description,
            item.FarmerProfile.Address,
            item.WholesalePrice,
            item.WholesaleMinQuantity
        }).SingleOrDefaultAsync(cancellationToken);
        if (information == null)
        {
            return null;
        }
        var images = await context.ProductImages.AsNoTracking()
            .Where(image => image.ProductId == productId)
            .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.DisplayOrder)
            .ThenBy(image => image.ProductImageId)
            .Select(image => new ProductDetailResponse.Image(image.ImageUrl, image.AltText))
            .ToListAsync(cancellationToken);
        var batches = await context.HarvestBatches.AsNoTracking()
            .Where(MarketplaceRules.AvailableBatch(Today))
            .Where(batch => batch.ProductId == productId)
            .OrderBy(batch => batch.ExpirationDate == null).ThenBy(batch => batch.ExpirationDate)
            .ThenBy(batch => batch.HarvestBatchId)
            .Select(batch => new ProductDetailResponse.Batch(
                batch.HarvestBatchId, batch.BatchCode, batch.AvailableQuantity,
                batch.CropSeason.SeasonName, batch.CropSeason.CropVariety,
                batch.ActualHarvestDate, batch.ExpirationDate, batch.QualityNotes,
                batch.TraceabilityQrcode != null && !batch.TraceabilityQrcode.IsRevoked
                    ? batch.TraceabilityQrcode.TargetUrl : null))
            .ToListAsync(cancellationToken);
        var certificates = await context.FarmCertificates.AsNoTracking()
            .Where(MarketplaceRules.ValidCertificate(Today))
            .Where(certificate => certificate.FarmerProfileId == product.SupplierId)
            .OrderBy(certificate => certificate.CertificateType)
            .Select(certificate => new ProductDetailResponse.Certificate(
                certificate.CertificateType, certificate.CertificateNumber,
                certificate.IssuedBy, certificate.ExpiryDate))
            .ToListAsync(cancellationToken);
        var reviews = await context.ProductReviews.AsNoTracking()
            .Where(MarketplaceRules.PublicReview)
            .Where(review => review.OrderItem.ProductId == productId)
            .OrderByDescending(review => review.ReviewDate)
            .ThenByDescending(review => review.ProductReviewId)
            // shortcut: latest 50 reviews; add paging when older reviews need browsing.
            .Take(50)
            .Select(review => new ProductDetailResponse.Review(
                review.ProductReviewId, review.User.FullName, review.RatingStars,
                review.Comment, review.ReviewDate))
            .ToListAsync(cancellationToken);
        var similar = await Summaries(PublicProducts().Where(item =>
                item.CategoryId == product.CategoryId && item.ProductId != productId))
            .OrderByDescending(item => item.Rating).ThenByDescending(item => item.Id)
            .Take(3).ToListAsync(cancellationToken);
        return new ProductDetailResponse(
            product, information.Description, information.Address, true,
            information.WholesalePrice, information.WholesaleMinQuantity,
            images, batches, certificates, reviews, similar);
    }

    private IQueryable<Product> PublicProducts() =>
        context.Products.AsNoTracking().Where(MarketplaceRules.PublicProduct);

    private IQueryable<ProductSummaryResponse> Summaries(IQueryable<Product> products)
    {
        var batches = context.HarvestBatches.Where(MarketplaceRules.AvailableBatch(Today));
        var certificates = context.FarmCertificates.Where(MarketplaceRules.ValidCertificate(Today));
        var reviews = context.ProductReviews.Where(MarketplaceRules.PublicReview);
        return products.Select(product => new ProductSummaryResponse
        {
            Id = product.ProductId,
            Name = product.ProductName,
            CategoryId = product.CategoryId,
            Category = product.Category.CategoryName,
            SupplierId = product.FarmerProfileId,
            Supplier = product.FarmerProfile.FarmName,
            Region = product.OriginProvince,
            Price = product.BaseRetailPrice,
            Unit = product.Unit,
            ImageUrl = context.ProductImages.Where(image => image.ProductId == product.ProductId)
                .OrderByDescending(image => image.IsPrimary).ThenBy(image => image.DisplayOrder)
                .ThenBy(image => image.ProductImageId)
                .Select(image => image.ImageUrl).FirstOrDefault(),
            AvailableQuantity = batches.Where(batch => batch.ProductId == product.ProductId)
                .Sum(batch => (decimal?)batch.AvailableQuantity) ?? 0,
            Rating = reviews.Where(review => review.OrderItem.ProductId == product.ProductId)
                .Average(review => (double?)review.RatingStars),
            ReviewCount = reviews.Count(review => review.OrderItem.ProductId == product.ProductId),
            Certifications = certificates.Where(certificate =>
                    certificate.FarmerProfileId == product.FarmerProfileId)
                .Select(certificate => certificate.CertificateType).Distinct().ToList()
        });
    }
}
