namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutContextDto(IReadOnlyList<CheckoutLine> SelectedItems,
    IReadOnlyList<CheckoutAddress> Addresses, long AvailablePoints,
    IReadOnlyList<string> VoucherCodes);
