using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface ICheckoutService
{
    Task<UseCaseResult<CheckoutContextDto>> GetContextAsync(CancellationToken cancellationToken);
    IReadOnlyList<DeliveryArea> GetAreas();

    /// <summary>Stores an owned price snapshot without reserving inventory or discounts.</summary>
    Task<UseCaseResult<CheckoutQuoteDto>> QuoteAsync(CheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates the quote and atomically reserves stock, discounts and creates the order.
    /// Retrying the same request key or quote returns the previously committed owned order.
    /// </summary>
    Task<UseCaseResult<OrderDto>> PlaceAsync(PlaceOrderRequest request,
        CancellationToken cancellationToken);
    Task<UseCaseResult<OrderDto>> GetOrderAsync(long orderId, CancellationToken cancellationToken);
}
