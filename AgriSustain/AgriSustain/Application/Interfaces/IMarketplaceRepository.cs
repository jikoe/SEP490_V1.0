using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces;

public interface IMarketplaceRepository
{
    Task<MarketplacePageResponse> BrowseAsync(
        BrowseMarketplaceRequest request, CancellationToken cancellationToken);
    Task<MarketplaceFiltersResponse> GetFiltersAsync(CancellationToken cancellationToken);
    Task<ProductDetailResponse?> GetDetailAsync(
        long productId, CancellationToken cancellationToken);
}
