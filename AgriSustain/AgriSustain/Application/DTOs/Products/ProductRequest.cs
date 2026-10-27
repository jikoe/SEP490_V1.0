namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductRequest(string ProductName, long CategoryId, string Unit,
    string Description, decimal BaseRetailPrice,
    decimal? WholesaleMinQuantity, decimal? WholesalePrice,
    IReadOnlyList<ProductImageInput> Images, string? Version = null);

