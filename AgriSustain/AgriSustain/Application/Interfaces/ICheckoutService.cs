using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface ICheckoutService
{
    /// <summary>
    /// Opens the checkout screen: the selected cart lines with their current unit price, the customer's active
    /// delivery addresses (default first), the reward point balance and the vouchers the customer may pick. A farm voucher
    /// is offered only when that farm is in the cart; each voucher says whether it applies to the current lines.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The checkout context, or 403 FORBIDDEN when the caller is not an active, unlocked Retail Customer.</returns>
    Task<UseCaseResult<CheckoutContextDto>> GetContextAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Delivery areas configured by the owner (CheckoutPolicy.Areas); never taken from the browser.
    /// </summary>
    /// <returns>Configured areas.</returns>
    IReadOnlyList<DeliveryArea> GetAreas();

    /// <summary>
    /// Stores an owned price snapshot without reserving inventory or discounts.
    /// </summary>
    /// <param name="request">Checkout form values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The quote, or the rule that failed.</returns>
    Task<UseCaseResult<CheckoutQuoteDto>> QuoteAsync(CheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates the quote and atomically reserves stock, discounts and creates the order.
    /// Retrying the same request key or quote returns the previously committed owned order.
    /// </summary>
    /// <param name="request">Quote id, request key and confirmation.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The order, or the error.</returns>
    Task<UseCaseResult<OrderDto>> PlaceAsync(PlaceOrderRequest request,
        CancellationToken cancellationToken);
    /// <summary>
    /// Reads one retail order of the current customer (used after placement and by the payment page).
    /// </summary>
    /// <param name="orderId">Order id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The order, or 403 / 404.</returns>
    Task<UseCaseResult<OrderDto>> GetOrderAsync(long orderId, CancellationToken cancellationToken);
}
