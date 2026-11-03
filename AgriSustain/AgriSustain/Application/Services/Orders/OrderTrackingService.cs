using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Orders;

/// <summary>UC-RC-04 Track Retail Order (read-only; only the buyer's own orders are visible).</summary>
public sealed class OrderTrackingService(
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IValidator<OrderQuery> queryValidator,
    TimeProvider timeProvider) : IOrderTrackingService
{
    /// <summary>Signed-in user id from the validated JWT, or null for an anonymous caller
    /// (ICurrentActor reports 0 when nobody is signed in).</summary>
    private long? SignedInUserId() => currentActor.UserId > 0 ? currentActor.UserId : null;

    /// <summary>Complaint window after delivery (GBR-24).</summary>
    public static readonly TimeSpan ComplaintWindow = TimeSpan.FromHours(48);

    private static readonly Error s_signInRequired = new(
        "UNAUTHORIZED", "Vui lòng đăng nhập để xem đơn hàng của bạn.", "Unauthorized");

    public async Task<Result<PagedResponse<OrderSummaryDto>>> GetOrderHistoryAsync(
        OrderQuery query, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(queryValidator, query, cancellationToken);
        if (error != null)
        {
            return Result<PagedResponse<OrderSummaryDto>>.FailureResult(error);
        }

        return await unitOfWork.ReadAsync(async uow =>
        {
            if (SignedInUserId() is not long buyerId)
            {
                return Result<PagedResponse<OrderSummaryDto>>.FailureResult(s_signInRequired);
            }

            var pagedOrders = await uow.Orders.GetOrdersByBuyerAsync(buyerId, query, cancellationToken);
            return Result<PagedResponse<OrderSummaryDto>>.SuccessResult(pagedOrders);
        }, cancellationToken);
    }

    public Task<Result<OrderDetailDto>> GetOrderDetailAsync(
        long orderId, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            if (SignedInUserId() is not long buyerId)
            {
                return Result<OrderDetailDto>.FailureResult(s_signInRequired);
            }

            // Ownership is part of the query: another buyer's order is reported as not found (no data leak).
            var order = await uow.Orders.GetOrderDetailAsync(buyerId, orderId, cancellationToken);
            return await CompleteAsync(uow, buyerId, order, $"Không tìm thấy đơn hàng #{orderId}.", cancellationToken);
        }, cancellationToken);

    public Task<Result<OrderDetailDto>> GetOrderDetailByCodeAsync(
        string orderCode, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            if (SignedInUserId() is not long buyerId)
            {
                return Result<OrderDetailDto>.FailureResult(s_signInRequired);
            }

            string code = (orderCode ?? "").Trim().TrimStart('#');
            var order = await uow.Orders.GetOrderDetailByCodeAsync(buyerId, code, cancellationToken);
            return await CompleteAsync(uow, buyerId, order, $"Không tìm thấy đơn hàng có mã {code}.", cancellationToken);
        }, cancellationToken);

    public Task<Result<string>> ConfirmReceivedAsync(
        long orderId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async uow =>
        {
            if (SignedInUserId() is not long buyerId)
            {
                return Result<string>.FailureResult(s_signInRequired);
            }

            var order = await uow.Orders.GetOrderForReceiptAsync(buyerId, orderId, cancellationToken);
            if (order == null)
            {
                return Result<string>.FailureResult(
                    new Error("ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng #{orderId}.", "NotFound"));
            }

            if (!CanConfirmReceipt(order))
            {
                return Result<string>.FailureResult(new Error("ORDER_NOT_DELIVERED",
                    "Chỉ có thể xác nhận đã nhận hàng khi đơn đang vận chuyển hoặc đã giao.", "Conflict"));
            }

            if (order.SellerOrders.Any(seller => seller.DisputeTickets.Any(ticket =>
                    ticket.TicketStatus is "Open" or "Reviewing")))
            {
                return Result<string>.FailureResult(new Error("COMPLAINT_IN_PROGRESS",
                    "Đơn hàng đang có khiếu nại chưa xử lý xong, chưa thể xác nhận đã nhận hàng.", "Conflict"));
            }

            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            ApplyReceipt(order, buyerId, now);
            return Result<string>.SuccessResult(order.OrderCode);
        }, cancellationToken);

    private static readonly string[] s_receivable = ["Shipping", "InTransit", "Delivered", "Received"];

    /// <summary>The order is out for delivery or delivered, and every live seller order has left the farm.</summary>
    public static bool CanConfirmReceipt(Domain.Entities.Order order) =>
        order.OrderStatus is "Shipping" or "Delivered"
        && order.SellerOrders.Any(seller => s_receivable.Contains(seller.Status))
        && order.SellerOrders.All(seller => s_receivable.Contains(seller.Status)
            || seller.Status is "Cancelled" or "Rejected");

    /// <summary>Delivered seller orders become Completed with ReceivedAt; the parent order becomes Completed.</summary>
    public static void ApplyReceipt(Domain.Entities.Order order, long buyerId, DateTime now)
    {
        foreach (var seller in order.SellerOrders.Where(seller => s_receivable.Contains(seller.Status)))
        {
            seller.DeliveredAt ??= now;
            foreach (var shipment in seller.Shipments.Where(x => x.ShipmentStatus is "InTransit" or "Shipping" or "Delivered"))
            {
                shipment.ShipmentStatus = "Delivered";
                shipment.DeliveredAt ??= now;
                shipment.ReceivedAt = now;
            }

            seller.OrderStatusHistories.Add(new Domain.Entities.OrderStatusHistory
            {
                SellerOrderId = seller.SellerOrderId,
                ChangedByUserId = buyerId,
                OldStatus = seller.Status,
                NewStatus = "Completed",
                Reason = "Người mua xác nhận đã nhận hàng",
                OccurredAt = now
            });
            seller.Status = "Completed";
            seller.ReceivedAt = now;
        }

        order.OrderStatus = "Completed";
        order.CompletedAt = now;
    }

    /// <summary>Contextual actions for the Order Details screen (SRS UC-RC-04 alternative flows, step 7).</summary>
    public static IReadOnlyList<string> ResolveActions(OrderDetailDto order, DateTimeOffset now)
    {
        var actions = new List<string>();
        if (order.OrderStatus == "PendingPayment" && order.PaymentStatus != "Paid"
            && (order.ReservationExpiresAt is null || order.ReservationExpiresAt > now))
        {
            actions.Add(OrderActions.PayNow);
        }

        if (order.OrderStatus == "Completed"
            && order.SellerOrders.Any(seller => seller.Status == "Completed"
                && seller.Items.Any(item => !item.IsReviewed)))
        {
            actions.Add(OrderActions.Review);
        }

        if (order.OrderStatus == "Completed"
            && order.SellerOrders.Any(seller => seller.Status == "Completed"
                && seller.Items.Any(item => item.IsReviewed)))
        {
            actions.Add(OrderActions.EditReview);
        }

        if (order.OrderStatus == "Delivered" && order.DeliveredAt is { } deliveredAt
            && deliveredAt <= now && now - deliveredAt <= ComplaintWindow)
        {
            actions.Add(OrderActions.SubmitComplaint);
        }

        if (order.OrderStatus is "Shipping" or "Delivered"
            && order.SellerOrders.Any(seller => s_receivable.Contains(seller.Status))
            && order.SellerOrders.All(seller => s_receivable.Contains(seller.Status)
                || seller.Status is "Cancelled" or "Rejected"))
        {
            actions.Add(OrderActions.ConfirmReceived);
        }

        return actions;
    }

    private async Task<Result<OrderDetailDto>> CompleteAsync(
        IUnitOfWork uow, long buyerId, OrderDetailDto? order, string notFoundMessage,
        CancellationToken cancellationToken)
    {
        if (order == null)
        {
            return Result<OrderDetailDto>.FailureResult(new Error("ORDER_NOT_FOUND", notFoundMessage, "NotFound"));
        }

        await uow.Orders.MarkOrderNotificationsReadAsync(buyerId, order.OrderId, cancellationToken);
        return Result<OrderDetailDto>.SuccessResult(order with
        {
            AllowedActions = ResolveActions(order, timeProvider.GetUtcNow())
        });
    }
}
