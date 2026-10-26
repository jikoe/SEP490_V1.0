namespace AgriSustain.Application.DTOs.Marketplace;

public sealed class ProductSummaryResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public long CategoryId { get; init; }
    public string Category { get; init; } = string.Empty;
    public long SupplierId { get; init; }
    public string Supplier { get; init; } = string.Empty;
    public string? Region { get; init; }
    public string? ImageUrl { get; init; }
    public decimal Price { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal AvailableQuantity { get; init; }
    public double? Rating { get; init; }
    public int ReviewCount { get; init; }
    public List<string> Certifications { get; init; } = [];
}
