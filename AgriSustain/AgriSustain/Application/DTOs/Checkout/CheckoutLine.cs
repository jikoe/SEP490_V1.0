namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutLine(long CartItemId, long ProductId, long FarmerId,
    string ProductName, string SellerName, string Unit, decimal Quantity, decimal UnitPrice,
    decimal Subtotal, string? ImageUrl, IReadOnlyList<BatchAllocation> Allocations);
