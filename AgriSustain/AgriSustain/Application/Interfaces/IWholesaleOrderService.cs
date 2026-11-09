using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.B2B;

namespace AgriSustain.Application.Interfaces;

public interface IWholesaleOrderService
{
    Task<Result<WholesaleProductInfoDto>> GetWholesaleProductInfoAsync(long productId, long buyerUserId, CancellationToken cancellationToken);
    Task<Result<WholesalePriceCalculationResponse>> CalculatePricingAsync(WholesalePriceCalculationRequest request, CancellationToken cancellationToken);
    Task<Result<WholesaleOrderConfirmationDto>> CreateWholesaleOrderAsync(CreateWholesaleOrderRequest request, long buyerUserId, CancellationToken cancellationToken);
    Task<Result<WholesaleBuyerOrdersOverviewDto>> GetBuyerWholesaleOrdersOverviewAsync(long buyerUserId, CancellationToken cancellationToken);
    Task<Result<WholesaleOrderListItemDto>> GetWholesaleOrderDetailByCodeAsync(string orderCode, CancellationToken cancellationToken);
    Task<Result<WholesaleOrderListItemDto>> ConfirmSePayWholesaleOrderAsync(string orderCode, CancellationToken cancellationToken);
    Task<Result<WholesaleOrderListItemDto>> CancelExpiredWholesaleOrderAsync(string orderCode, CancellationToken cancellationToken);
}

