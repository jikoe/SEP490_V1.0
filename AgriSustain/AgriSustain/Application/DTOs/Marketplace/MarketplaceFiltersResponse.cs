namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record MarketplaceFiltersResponse(
    IReadOnlyList<MarketplaceFiltersResponse.FilterOption> Categories,
    IReadOnlyList<MarketplaceFiltersResponse.FilterOption> Suppliers,
    IReadOnlyList<MarketplaceFiltersResponse.FilterOption> Seasons,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Certifications)
{
    public sealed record FilterOption(long Id, string Name, int Count);
}
