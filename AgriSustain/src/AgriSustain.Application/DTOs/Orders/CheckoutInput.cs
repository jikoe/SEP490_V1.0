using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.DTOs.Orders;

public sealed record CheckoutInput(
    AddressSnapshot Address, string DeliveryMethod = "Standard",
    string PaymentMethod = "PayOS", string? VoucherCode = null,
    int Points = 0, string? Notes = null);
