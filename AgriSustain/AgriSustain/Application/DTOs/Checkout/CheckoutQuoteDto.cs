namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutQuoteDto(Guid Id, CheckoutRequest Input,
    IReadOnlyList<CheckoutLine> Lines, decimal Subtotal, decimal DeliveryFee,
    decimal VoucherDiscount, decimal PointsDiscount, decimal Total, DateTimeOffset ExpiresAt,
    DateTimeOffset ExpectedDeliveryAt, long? VoucherId, long? VoucherFarmerId, string? Warning,
    IReadOnlyDictionary<long, decimal>? SellerDeliveryFees = null);
