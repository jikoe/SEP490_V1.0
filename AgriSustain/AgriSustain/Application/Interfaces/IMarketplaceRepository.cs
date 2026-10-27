using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces;

public interface IMarketplaceRepository
{
    Task<MarketplacePageResponse> BrowseAsync(
        BrowseMarketplaceRequest request, CancellationToken cancellationToken);
    Task<MarketplaceFiltersResponse> GetFiltersAsync(CancellationToken cancellationToken,
        long? categoryId = null, long? supplierId = null);
    Task<ProductDetailResponse?> GetDetailAsync(
        long productId, CancellationToken cancellationToken);
}
