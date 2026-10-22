namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record ProductDetailResponse(
    ProductSummaryResponse Product,
    string? Description,
    string FarmAddress,
    bool IsFarmVerified,
    decimal? WholesalePrice,
    decimal? WholesaleMinQuantity,
    IReadOnlyList<ProductDetailResponse.Image> Images,
    IReadOnlyList<ProductDetailResponse.Batch> Batches,
    IReadOnlyList<ProductDetailResponse.Certificate> Certificates,
    IReadOnlyList<ProductDetailResponse.Review> Reviews,
    IReadOnlyList<ProductSummaryResponse> SimilarProducts)
{
    public sealed record Image(string Url, string? AltText);
    public sealed record Batch(
        long Id, string Code, decimal AvailableQuantity, string Season, string Variety,
        DateOnly? HarvestDate, DateOnly? ExpirationDate, string? QualityNotes,
        string? TraceabilityUrl);
    public sealed record Certificate(
        string Type, string Number, string IssuedBy, DateOnly? ExpiryDate);
    public sealed record Review(
        long Id, string Author, byte Rating, string? Comment, DateTime Date);
}
