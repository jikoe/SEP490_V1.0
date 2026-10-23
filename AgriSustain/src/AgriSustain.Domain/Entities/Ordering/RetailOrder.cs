using AgriSustain.Domain.Exceptions;
using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Domain.Entities.Ordering;

public sealed class RetailOrder
{
    public required long Id
    {
        get; init;
    }
    public required long BuyerId
    {
        get; init;
    }
    public required string Code
    {
        get; init;
    }
    public Guid QuoteId
    {
        get; init;
    }

    // DEMO-RETRY: process-local behavior only; DB-06 SQL contract remains undecided.
    public required Guid RequestKey
    {
        get; init;
    }
    public required string RequestFingerprint
    {
        get; init;
    }
    public required AddressSnapshot Address
    {
        get; init;
    }
    public required string DeliveryMethod
    {
        get; init;
    }
    public required string PaymentMethod
    {
        get; init;
    }
    public string? Notes
    {
        get; init;
    }
    public required DateTimeOffset CreatedAt
    {
        get; init;
    }
    public required DateTimeOffset ReservationExpiresAt
    {
        get; init;
    }
    public string Status => "PendingPayment";
    public required IReadOnlyList<OrderLineSnapshot> Lines
    {
        get; init;
    }
    public required IReadOnlyList<SellerOrderSnapshot> Sellers
    {
        get; init;
    }
    public required decimal DeliveryFee
    {
        get; init;
    }
    public required decimal VoucherDiscount
    {
        get; init;
    }
    public required decimal PointsDiscount
    {
        get; init;
    }
    public required int RedeemedPoints
    {
        get; init;
    }
    public long? VoucherId
    {
        get; init;
    }
    public decimal Subtotal => Lines.Sum(line => line.Subtotal);
    public decimal Total => Subtotal + DeliveryFee - VoucherDiscount - PointsDiscount;

    /// <summary>
    /// Verifies positive money/quantities, nonempty allocations whose sum matches each product
    /// line, and seller totals matching the parent payable amount before the order is staged.
    /// </summary>
    public void EnsureValid()
    {
        if (Lines.Count == 0 || Total <= 0 || RequestKey == Guid.Empty
            || VoucherDiscount < 0 || PointsDiscount < 0 || DeliveryFee < 0
            || Lines.Any(line => line.Quantity <= 0 || line.UnitPrice <= 0
                || line.Allocations.Count == 0
                || line.Allocations.Any(allocation => allocation.Quantity <= 0)
                || line.Allocations.Sum(allocation => allocation.Quantity) != line.Quantity)
            || Sellers.Sum(seller => seller.FinalAmount) != Total)
        {
            throw new BusinessRuleException("Tổng tiền và chi tiết đơn hàng không hợp lệ.");
        }
    }
}
