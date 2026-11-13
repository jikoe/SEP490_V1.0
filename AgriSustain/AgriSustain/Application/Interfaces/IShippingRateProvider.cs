using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface IShippingRateProvider
{
    int EstimatedDeliveryDays(string deliveryMethod);

    Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken);

    /// <summary>UC-RC-02 Step 5: true when the farm has a pickup address to quote from
    /// (its Farmer profile, or a provider-specific fallback).</summary>
    bool HasPickupAddress(long farmerProfileId, ShippingPickup? pickup) => true;
}
