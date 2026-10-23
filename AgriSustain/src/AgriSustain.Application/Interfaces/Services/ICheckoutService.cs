using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces.Services;

public interface ICheckoutService
{
    /// <summary>
    /// Loads only the active RetailCustomer own selected-cart context, saved addresses and mock
    /// benefits. It consumes UC-RC-01 output without adding cart CRUD behavior.
    /// </summary>
    Task<Result<CommerceContext>> GetContextAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Validates checkout input, builds a server quote and saves it for the buyer. Preview does
    /// not reserve inventory; invalid voucher feedback removes only its discount.
    /// </summary>
    Task<Result<CheckoutQuote>> PreviewAsync(
        CheckoutInput input, CancellationToken cancellationToken);
    /// <summary>
    /// Revalidates the owned quote, prices, FEFO allocation and benefits, then atomically stages
    /// batch reservations, order snapshots, selected-cart removal and audit. Retry/points storage
    /// remains explicitly mocked pending DB-06/09 decisions.
    /// </summary>
    Task<Result<OrderResponse>> PlaceAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken);
}
