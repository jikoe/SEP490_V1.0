using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.DTOs.Orders;

public sealed record OrderResponse(
    long Id, string Code, string Status, DateTimeOffset ReservationExpiresAt,
    decimal Subtotal, decimal DeliveryFee, decimal VoucherDiscount, decimal PointsDiscount,
    decimal Total, IReadOnlyList<SellerOrderSnapshot> Sellers, bool IsDuplicate,
    string? PaymentDestination, string Message);
