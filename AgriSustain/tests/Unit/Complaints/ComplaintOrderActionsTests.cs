using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.Complaints;

[Trait("UseCase", "UC-RC-06")]
public sealed class ComplaintOrderActionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    private static OrderDetailDto CompletedOrder(DateTimeOffset? deliveredAt, bool hasComplaint = false)
    {
        var seller = new SellerOrder { Status = "Completed", DeliveredAt = deliveredAt?.UtcDateTime };
        seller.OrderItems.Add(new OrderItem
        {
            SellerOrder = seller, OrderItemId = 1, ItemStatus = "Normal", Quantity = 1
        });
        if (hasComplaint)
        {
            seller.DisputeTickets.Add(new DisputeTicket
            {
                TicketStatus = DisputeTicket.StatusWaitingInfo
            });
        }
        var order = new Order { OrderType = "Retail", OrderStatus = "Completed", SellerOrders = [seller] };
        return new OrderDetailDto(
            1, "UT-ORDER-1", Now.AddDays(-1), order.OrderStatus, "PayOS", "Standard",
            100000, 0, 0, 100000, "Unit Buyer", "0900000000", "Unit address", null,
            "Paid", null, [], [], DeliveredAt: deliveredAt,
            CanSubmitComplaint: OrderFeedbackRules.HasComplaintEligibleItems(order, Now.UtcDateTime));
    }

    [Fact]
    public void ResolveActions_OnlyItemAlreadyHasOpenComplaint_HidesComplaint()
    {
        var order = CompletedOrder(Now.AddHours(-1), hasComplaint: true);

        Assert.DoesNotContain(OrderActions.SubmitComplaint, OrderTrackingService.ResolveActions(order, Now));
    }

    [Fact(DisplayName = "[UC-RC-06 #031] Completed order details show the complaint action within 48 hours")]
    public void ResolveActions_CompletedWithinWindow_ShowsComplaint()
    {
        var order = CompletedOrder(Now.AddHours(-1));

        var actions = OrderTrackingService.ResolveActions(order, Now);

        Assert.Contains(OrderActions.SubmitComplaint, actions);
    }

    [Fact(DisplayName = "[UC-RC-06 #032] Completed order details allow complaints at the exact delivery deadline")]
    public void ResolveActions_CompletedAtDeadline_ShowsComplaint()
    {
        var order = CompletedOrder(Now.AddHours(-48));

        var actions = OrderTrackingService.ResolveActions(order, Now);

        Assert.Contains(OrderActions.SubmitComplaint, actions);
    }

    [Fact(DisplayName = "[UC-RC-06 #033] Completed order details hide complaints after the delivery deadline")]
    public void ResolveActions_CompletedAfterDeadline_HidesComplaint()
    {
        var order = CompletedOrder(Now.AddHours(-48).AddTicks(-1));

        var actions = OrderTrackingService.ResolveActions(order, Now);

        Assert.DoesNotContain(OrderActions.SubmitComplaint, actions);
    }

    [Fact(DisplayName = "[UC-RC-06 #034] Completed order details require a delivery timestamp for complaints")]
    public void ResolveActions_CompletedWithoutDelivery_HidesComplaint()
    {
        var actions = OrderTrackingService.ResolveActions(CompletedOrder(null), Now);

        Assert.DoesNotContain(OrderActions.SubmitComplaint, actions);
    }

    [Fact(DisplayName = "[UC-RC-06 #035] Completed order details reject a future delivery timestamp")]
    public void ResolveActions_CompletedWithFutureDelivery_HidesComplaint()
    {
        var actions = OrderTrackingService.ResolveActions(CompletedOrder(Now.AddTicks(1)), Now);

        Assert.DoesNotContain(OrderActions.SubmitComplaint, actions);
    }
}
