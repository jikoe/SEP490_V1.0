using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.DTOs.Orders;

public sealed record CommerceContext(
    IReadOnlyList<CartLine> Items, IReadOnlyList<AddressSnapshot> Addresses,
    int AvailablePoints, IReadOnlyList<string> VoucherCodes,
    IReadOnlyList<OrderLineSnapshot>? SelectedItems = null);
