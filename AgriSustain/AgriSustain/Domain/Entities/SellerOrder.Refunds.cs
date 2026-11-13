namespace AgriSustain.Domain.Entities;

/// <summary>
/// UC-CS-01 / UC-FIN-03: refunds are decided and settled per seller order (one farm's part of the
/// order), so the refund ceiling and the Refunded status use this seller order's paid amount.
/// </summary>
public partial class SellerOrder
{
    public const string StatusRefundPending = "RefundPending";
    public const string StatusRefunded = "Refunded";
    public const string StatusPartiallyRefunded = "PartiallyRefunded";

    /// <summary>
    /// Refunds of this seller order that still hold money: requested, in transfer or completed (BR-CS-04).
    /// </summary>
    public decimal ReservedRefundAmount => ComplaintRefunds()
        .Where(refund => refund.ReservesPayment)
        .Sum(refund => refund.Amount);

    /// <summary>
    /// Refunds of this seller order already transferred; decides Refunded vs Partially Refunded.
    /// </summary>
    public decimal CompletedRefundAmount => ComplaintRefunds()
        .Where(refund => refund.RefundStatus == RefundTransaction.StatusCompleted)
        .Sum(refund => refund.Amount);

    /// <summary>What the buyer paid for one line: its subtotal less its share of the seller order's discount.</summary>
    public decimal PaidValueOf(OrderItem item) => PaidValue(item.SubTotal, Subtotal, DiscountShare);

    /// <summary>
    /// Paid value of a line = its subtotal minus its proportional share of the seller order's discount.
    /// </summary>
    /// <param name="lineSubtotal">Line subtotal.</param>
    /// <param name="sellerSubtotal">Seller order subtotal.</param>
    /// <param name="discountShare">Discount carried by this seller order.</param>
    /// <returns>Rounded VND amount; the plain subtotal when the seller subtotal is 0.</returns>
    public static decimal PaidValue(decimal lineSubtotal, decimal sellerSubtotal, decimal discountShare) =>
        sellerSubtotal <= 0
            ? lineSubtotal
            : RetailPricing.Money(lineSubtotal - discountShare * lineSubtotal / sellerSubtotal);

    /// <summary>The refund for part of a line, in proportion to the quantity returned or affected.</summary>
    public decimal RefundValueOf(OrderItem item, decimal quantity) => item.Quantity <= 0
        ? 0
        : RetailPricing.Money(PaidValueOf(item) * quantity / item.Quantity);

    /// <summary>
    /// Refunds requested or made for complaints about this seller order. A method, not a
    /// property, so EF Core does not map it as a navigation.
    /// </summary>
    private IEnumerable<RefundTransaction> ComplaintRefunds() => Order.PaymentTransactions
        .SelectMany(payment => payment.RefundTransactions)
        .Where(BelongsToThisSellerOrder);

    private bool BelongsToThisSellerOrder(RefundTransaction refund) =>
        refund.DisputeTicket?.SellerOrderId == SellerOrderId
        || refund.DisputeTicket?.SellerOrder == this
        || refund.DisputeTicketId is long ticketId
            && DisputeTickets.Any(ticket => ticket.DisputeTicketId == ticketId);
}
