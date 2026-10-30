namespace AgriSustain.Application.DTOs.Products;

public sealed record ProductResponse(long ProductId, string ProductName, long CategoryId,
    string CategoryName, string Unit, string? Description,
    decimal BaseRetailPrice, decimal? WholesaleMinQuantity, decimal? WholesalePrice,
    string ApprovalStatus, bool IsPublished, string? RejectionReason,
    decimal AvailableQuantity, int BatchCount, bool UnitLocked, string Version,
    IReadOnlyList<ProductImageInput> Images)
{
    public string? Message { get; init; }
    public bool NotificationPending { get; init; }
    public string? NotificationMessage { get; init; }
    public IReadOnlyList<ProductBatchSummary> Batches { get; init; } = [];
}
public sealed record ProductBatchSummary(string BatchCode, string Status,
    decimal AvailableQuantity, DateOnly? ExpirationDate);
