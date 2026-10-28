namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductResponse(long ProductId, string ProductName, long CategoryId,
    string CategoryName, string Unit, string? Description,
    decimal BaseRetailPrice, decimal? WholesaleMinQuantity, decimal? WholesalePrice,
    string ApprovalStatus, bool IsPublished, string? RejectionReason,
    decimal AvailableQuantity, int BatchCount, bool UnitLocked, string Version,
    IReadOnlyList<ProductImageInput> Images);

