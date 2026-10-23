using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces.Services;

public interface IOrderTrackingService
{
    Task<Result<PagedResponse<OrderSummaryDto>>> GetOrderHistoryAsync(
        OrderQuery query, CancellationToken cancellationToken);

    Task<Result<OrderDetailDto>> GetOrderDetailAsync(
        long orderId, CancellationToken cancellationToken);

    Task<Result<OrderDetailDto>> GetOrderDetailByCodeAsync(
        string orderCode, CancellationToken cancellationToken);
}
