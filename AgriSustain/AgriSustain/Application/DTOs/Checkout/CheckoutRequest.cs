namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutRequest(CheckoutAddress Address, string DeliveryMethod,
    string PaymentMethod, string? VoucherCode, int Points, string? Notes);
