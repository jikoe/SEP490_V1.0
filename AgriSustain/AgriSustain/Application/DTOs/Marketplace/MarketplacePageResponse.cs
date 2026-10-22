namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record MarketplacePageResponse(
    IReadOnlyList<ProductSummaryResponse> Items, int Total, int Page, int PageSize);
