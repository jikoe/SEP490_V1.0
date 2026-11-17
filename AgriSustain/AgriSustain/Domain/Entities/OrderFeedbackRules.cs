namespace AgriSustain.Domain.Entities;

public static class OrderFeedbackRules
{
    private const string ComplaintItemMarker = "Sản phẩm khiếu nại (dòng hàng #";

    public static bool CanReview(Order order, OrderItem item) =>
        IsReceived(order, item)
        && item.ProductReview == null;

    public static bool CanEditReview(Order order, OrderItem item, long userId) =>
        IsReceived(order, item)
        && item.ProductReview?.UserId == userId;

    private static bool IsReceived(Order order, OrderItem item) =>
        order.OrderStatus == "Completed"
        && item.SellerOrder.Status == "Completed"
        && item.SellerOrder.ReceivedAt != null
        && item.Quantity > 0
        && item.ItemStatus != "Cancelled";

    public static bool HasComplaintEligibleItems(Order order, DateTime now) =>
        order.OrderType == "Retail" && order.SellerOrders.SelectMany(seller => seller.OrderItems)
            .Any(item => CanComplain(order, item, now));

    public static bool CanComplain(Order order, OrderItem item, DateTime now) =>
        order.OrderStatus is ("Delivered" or "Completed")
        && item.SellerOrder.Status is ("Delivered" or "Completed")
        && item.ItemStatus != "Cancelled"
        && item.Quantity > 0
        && DeliveredAt(item.SellerOrder) is { } deliveredAt
        && deliveredAt <= now && now <= deliveredAt.AddHours(48)
        && !item.SellerOrder.DisputeTickets.Any(ticket =>
            (ticket.IsAwaitingDecision || ticket.TicketStatus == "Reviewing")
            && ConcernsItem(ticket, item.OrderItemId));

    public static string DescribeComplaint(IEnumerable<OrderItem> items, string description) =>
        string.Join("\n", items.OrderBy(item => item.OrderItemId).Select(item =>
            ComplaintItemPrefix(item.OrderItemId)
            + (item.ProductNameSnapshot ?? item.Product.ProductName).ReplaceLineEndings(" ")))
        + $"\n\n{description.Trim()}";

    private static string ComplaintItemPrefix(long itemId) =>
        FormattableString.Invariant($"{ComplaintItemMarker}{itemId}): ");

    private static bool ConcernsItem(DisputeTicket ticket, long itemId)
    {
        // Legacy complaints without an item reference apply to the whole seller order.
        if (ticket.Description == null
            || !ticket.Description.StartsWith(ComplaintItemMarker, StringComparison.Ordinal))
        {
            return true;
        }
        string products = ticket.Description.Replace("\r\n", "\n").Split("\n\n", 2)[0];
        return products.Split('\n').Any(line =>
            line.StartsWith(ComplaintItemPrefix(itemId), StringComparison.Ordinal));
    }

    public static DateTime? DeliveredAt(SellerOrder seller) => seller.DeliveredAt
        ?? seller.OrderStatusHistories.Where(history => history.NewStatus == "Delivered")
            .Select(history => (DateTime?)history.OccurredAt).Max();
}
