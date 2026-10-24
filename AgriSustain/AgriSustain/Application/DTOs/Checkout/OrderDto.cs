namespace AgriSustain.Application.DTOs.Checkout;

public sealed record OrderDto(long Id, string Code, string Status, decimal Total,
    DateTimeOffset ReservationExpiresAt, string PaymentMethod, string? PaymentPath,
    bool IsDuplicate = false, string? Message = null);
