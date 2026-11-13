using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface IShippingRateProvider
{
    /// <summary>
    /// Days until delivery for the method, used for the expected date and batch expiry check.
    /// </summary>
    /// <param name="deliveryMethod">Standard or FreshExpress.</param>
    /// <returns>Days.</returns>
    int EstimatedDeliveryDays(string deliveryMethod);

    /// <summary>
    /// Delivery fee of one farm's parcel.
    /// </summary>
    /// <param name="request">Farm, recipient, parcel lines, declared value, method and pickup.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Supported fee, unsupported route (IsSupported false), or null when the fee cannot be obtained.</returns>
    Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken);

    /// <summary>UC-RC-02 Step 5: true when the farm has a pickup address to quote from
    /// (its Farmer profile, or a provider-specific fallback).</summary>
    bool HasPickupAddress(long farmerProfileId, ShippingPickup? pickup) => true;
}
