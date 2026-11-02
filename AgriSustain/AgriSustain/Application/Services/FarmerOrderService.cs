using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

/// <summary>
/// «control» FarmerOrderService
/// Use Case: UC-FAR-06 — Process Order (revised)
/// Coordinates boundary interactions, business logic execution, and notifications.
/// </summary>
public sealed class FarmerOrderService(
    IFarmerOrderRepository repository,
    OrderFulfillmentService fulfillmentService)
{
    public Task<FeatureResult<PageResult<SellerOrderResponse>>> GetOrdersAsync(
        long actorId, SellerOrderQuery query, CancellationToken cancellationToken) =>
        fulfillmentService.GetListAsync(actorId, query, cancellationToken);

    public Task<FeatureResult<SellerOrderResponse>> GetOrderAsync(
        long actorId, long id, CancellationToken cancellationToken) =>
        fulfillmentService.GetAsync(actorId, id, cancellationToken);

    public Task<FeatureResult<SellerOrderResponse>> ProcessActionAsync(
        long actorId, long id, string action, OrderActionRequest request, CancellationToken cancellationToken) =>
        fulfillmentService.ActAsync(actorId, id, action, request, cancellationToken);

    public async Task<FeatureResult<SellerOrderResponse>> RejectOrderAsync(
        long actorId, long id, string reason, string requestKey, CancellationToken cancellationToken)
    {
        var existingOrder = await repository.GetOwnedAsync(actorId, id, cancellationToken);
        if (existingOrder is null)
        {
            return FeatureResult<SellerOrderResponse>.Fail("NOT_FOUND",
                "Bạn không có quyền truy cập đơn hàng này hoặc thực hiện thao tác.");
        }

        var request = new OrderActionRequest(
            Version: FulfillmentRules.Version(existingOrder),
            RequestKey: requestKey,
            Explanation: reason);

        return await fulfillmentService.ActAsync(actorId, id, "reject", request, cancellationToken);
    }

    public async Task<FeatureResult<PackingSlipResponse>> GetPackingSlipAsync(
        long actorId, long id, CancellationToken cancellationToken)
    {
        var order = await repository.GetOwnedAsync(actorId, id, cancellationToken);
        if (order is null)
        {
            return FeatureResult<PackingSlipResponse>.Fail("NOT_FOUND",
                "Bạn không có quyền truy cập đơn hàng này hoặc thực hiện thao tác.");
        }

        return FeatureResult<PackingSlipResponse>.Ok(fulfillmentService.BuildPackingSlip(order));
    }
}

