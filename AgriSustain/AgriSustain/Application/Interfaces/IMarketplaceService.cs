using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces;

public interface IMarketplaceService
{
    Task<Result<MarketplacePageResponse>> BrowseAsync(
        BrowseMarketplaceRequest request, CancellationToken cancellationToken);
    Task<Result<MarketplaceFiltersResponse>> GetFiltersAsync(CancellationToken cancellationToken,
        long? categoryId = null, long? supplierId = null);
    Task<Result<ProductDetailResponse>> GetDetailAsync(
        long productId, CancellationToken cancellationToken);
}
