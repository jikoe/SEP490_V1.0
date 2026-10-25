namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutContextDto(IReadOnlyList<CheckoutLine> SelectedItems,
    IReadOnlyList<CheckoutAddress> Addresses, long AvailablePoints,
    IReadOnlyList<VoucherOption> Vouchers);

/// <summary>
/// A voucher the buyer can pick on checkout. <see cref="Eligible"/> is a preview against the
/// selected items; the quote still validates the code on the server.
/// </summary>
public sealed record VoucherOption(string Code, string DiscountType, decimal DiscountValue,
    decimal? MaxDiscountAmount, decimal MinOrderAmount, DateTime EndDate,
    long? FarmerProfileId, string? FarmName, bool Eligible);
