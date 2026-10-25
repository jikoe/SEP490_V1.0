using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface IGhtkShippingRateProvider
{
    int EstimatedDeliveryDays(string deliveryMethod);

    Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken);
}
