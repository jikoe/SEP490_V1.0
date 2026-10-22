using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Orders;

public sealed class OrderTrackingService(
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<OrderQuery> queryValidator) : IOrderTrackingService
{
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
            long buyerId = currentUser.UserId ?? 1;
            var pagedOrders = await uow.Orders.GetOrdersByBuyerAsync(buyerId, query, cancellationToken);
            return Result<PagedResponse<OrderSummaryDto>>.SuccessResult(pagedOrders);
        }, cancellationToken);
    }

    public Task<Result<OrderDetailDto>> GetOrderDetailAsync(
        long orderId, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            long buyerId = currentUser.UserId ?? 1;
            var order = await uow.Orders.GetOrderDetailAsync(buyerId, orderId, cancellationToken);
            return order == null
                ? Result<OrderDetailDto>.FailureResult(new Error(
                    "ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng #{orderId}.", "NotFound"))
                : Result<OrderDetailDto>.SuccessResult(order);
        }, cancellationToken);

    public Task<Result<OrderDetailDto>> GetOrderDetailByCodeAsync(
        string orderCode, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async uow =>
        {
            long buyerId = currentUser.UserId ?? 1;
            var order = await uow.Orders.GetOrderDetailByCodeAsync(buyerId, orderCode, cancellationToken);
            return order == null
                ? Result<OrderDetailDto>.FailureResult(new Error(
                    "ORDER_NOT_FOUND", $"Không tìm thấy đơn hàng có mã {orderCode}.", "NotFound"))
                : Result<OrderDetailDto>.SuccessResult(order);
        }, cancellationToken);
}
