using AgriSustain.Domain.Entities;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IOrderTrackingRepository
{
    Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(long buyerId, OrderQuery query, CancellationToken cancellationToken);
    Task<OrderDetailDto?> GetOrderDetailAsync(long buyerId, long orderId, CancellationToken cancellationToken);
    Task<OrderDetailDto?> GetOrderDetailByCodeAsync(long buyerId, string orderCode, CancellationToken cancellationToken);

    /// <summary>Marks the buyer's notifications about this order as read (UC-RC-04 postcondition).</summary>
    Task MarkOrderNotificationsReadAsync(long buyerId, long orderId, CancellationToken cancellationToken);

    /// <summary>Loads the buyer's own order (tracked) with seller orders and their complaints, for Confirm Received.</summary>
    Task<Order?> GetOrderForReceiptAsync(long buyerId, long orderId, CancellationToken cancellationToken);
}
