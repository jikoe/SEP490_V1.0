using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.Interfaces.Services;

// Owned by delivery/rewards modules. Implementations must state their financial assumptions.
public interface ICheckoutPolicy
{
    /// <summary>
    /// Returns a shipping fee only for a configured fixture province code and method, including
    /// the demo multi-seller surcharge. This is shipping, not the separate ordinary 2%
    /// transaction commission.
    /// </summary>
    decimal? GetDeliveryFee(AddressSnapshot address, string method, int sellerCount);
    /// <summary>
    /// Converts requested fixture points and checks the provisional redemption cap. DB-09
    /// policy/storage is not approved; do not treat these sample rates as production loyalty
    /// rules.
    /// </summary>
    decimal? GetPointsDiscount(int points, decimal subtotalAfterVoucher);
    TimeSpan QuoteLifetime
    {
        get;
    }
    TimeSpan ReservationLifetime
    {
        get;
    }
    /// <summary>
    /// Returns the handoff route if the payment module supplies one; null in the demo explicitly
    /// means no QR or gateway has been implemented.
    /// </summary>
    string? GetPaymentDestination(long orderId);
}
