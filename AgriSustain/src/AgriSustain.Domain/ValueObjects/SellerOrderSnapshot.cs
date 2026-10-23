namespace AgriSustain.Domain.ValueObjects;

public sealed record SellerOrderSnapshot(
    long SellerId, string Code, decimal Subtotal, decimal DiscountShare,
    decimal DeliveryFee, decimal FinalAmount);
