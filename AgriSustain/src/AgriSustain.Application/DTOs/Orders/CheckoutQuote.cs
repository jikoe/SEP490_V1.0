using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.DTOs.Orders;

public sealed record CheckoutQuote(
    Guid Id, long BuyerId, CheckoutInput Input, string Fingerprint,
    IReadOnlyList<OrderLineSnapshot> Lines, decimal Subtotal, decimal DeliveryFee,
    decimal VoucherDiscount, decimal PointsDiscount, decimal Total, long? VoucherId,
    string? WarningCode, string? Warning, DateTimeOffset ExpiresAt, long? VoucherSellerId = null);
