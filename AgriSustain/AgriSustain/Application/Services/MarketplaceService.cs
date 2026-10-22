using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;

namespace AgriSustain.Application.Services;

public sealed class MarketplaceService(IMarketplaceRepository repository) : IMarketplaceService
{
    public async Task<Result<MarketplacePageResponse>> BrowseAsync(
        BrowseMarketplaceRequest request, CancellationToken cancellationToken)
    {
        if (!MarketplaceRequestValidator.IsValid(request))
        {
            return Result<MarketplacePageResponse>.Fail(
                "MSG40: Vui lòng nhập từ khóa hoặc giá trị bộ lọc hợp lệ.");
        }

        var page = await repository.BrowseAsync(request with
        {
            Keyword = request.Keyword?.Trim(), Region = request.Region?.Trim()
        }, cancellationToken);
        return Result<MarketplacePageResponse>.Ok(page, page.Total == 0
            ? "MSG01: Không tìm thấy sản phẩm phù hợp."
            : "Đã tải danh sách sản phẩm.");
    }

    public async Task<Result<MarketplaceFiltersResponse>> GetFiltersAsync(
        CancellationToken cancellationToken) =>
        Result<MarketplaceFiltersResponse>.Ok(await repository.GetFiltersAsync(cancellationToken));

    public async Task<Result<ProductDetailResponse>> GetDetailAsync(
        long productId, CancellationToken cancellationToken)
    {
        var detail = productId > 0
            ? await repository.GetDetailAsync(productId, cancellationToken)
            : null;
        return detail == null
            ? Result<ProductDetailResponse>.Fail("MSG16: Sản phẩm không còn khả dụng.", 404)
            : Result<ProductDetailResponse>.Ok(detail);
    }
}
