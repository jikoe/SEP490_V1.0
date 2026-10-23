namespace AgriSustain.Domain.ValueObjects;

public sealed record OrderLineSnapshot(
    long CartItemId, long ProductId, long SellerId, string SellerName,
    string ProductName, string Unit, decimal Quantity, decimal UnitPrice, decimal Subtotal,
    IReadOnlyList<BatchAllocationSnapshot> Allocations, string? ImageUrl = null);
