namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record BrowseMarketplaceRequest
{
    public string? Keyword { get; init; }
    public long? CategoryId { get; init; }
    public long? SupplierId { get; init; }
    public long? CropSeasonId { get; init; }
    public string? Region { get; init; }
    public string[] Certifications { get; init; } = [];
    public string[] PriceRanges { get; init; } = [];
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public double? MinRating { get; init; }
    public string Sort { get; init; } = "newest";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
}
