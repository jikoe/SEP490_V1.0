namespace AgriSustain.Domain.Entities;

public partial class Order
{
    /// <summary>
    /// The buyer sees one order status, derived from its seller orders after a refund decision:
    /// RefundPending while any part awaits Finance, Refunded when every non-cancelled part is
    /// refunded, otherwise PartiallyRefunded once any part has been refunded.
    /// </summary>
    public void SyncRefundStatus()
    {
        var parts = SellerOrders.Where(sellerOrder => sellerOrder.Status != "Cancelled").ToList();
        if (parts.Count == 0)
        {
            return;
        }

        if (parts.Any(sellerOrder => sellerOrder.Status == SellerOrder.StatusRefundPending))
        {
            OrderStatus = SellerOrder.StatusRefundPending;
        }
        else if (parts.All(sellerOrder => sellerOrder.Status == SellerOrder.StatusRefunded))
        {
            OrderStatus = SellerOrder.StatusRefunded;
        }
        else if (parts.Any(sellerOrder => sellerOrder.Status
            is SellerOrder.StatusRefunded or SellerOrder.StatusPartiallyRefunded))
        {
            OrderStatus = SellerOrder.StatusPartiallyRefunded;
        }
    }
}
