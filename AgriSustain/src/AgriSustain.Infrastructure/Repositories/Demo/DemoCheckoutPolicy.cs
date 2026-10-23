using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.ValueObjects;
using AgriSustain.Infrastructure.Data.Demo;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Repositories.Demo;

// DEMO-DELIVERY / DEMO-REWARDS / DEMO-PAYMENT:
// Fees, service areas, point conversion/cap and durations are fixture assumptions in
// appsettings.Development.json, NOT approved business policy. The real local geography
// catalog validates province/ward codes separately; coverage here is a province-code fixture.
// No carrier API or payment request is made; shipping is separate from transaction commission.
// Replace ICheckoutPolicy with the team's delivery + loyalty + payment-handoff adapters.
// Reserve/apply/release benefits must share the SQL transaction/outbox contract.
/// <summary>
/// Loads explicit Development-only fee, service-area, points and duration assumptions. The
/// dependency teams must replace these policies with reviewed contracts.
/// </summary>
public sealed class DemoCheckoutPolicy(IOptions<DemoCommerceOptions> options) : ICheckoutPolicy
{
    private readonly DemoCommerceOptions _options = options.Value;

    public TimeSpan QuoteLifetime => TimeSpan.FromMinutes(_options.QuoteMinutes);
    public TimeSpan ReservationLifetime => TimeSpan.FromMinutes(_options.ReservationMinutes);

    /// <summary>
    /// Returns a shipping fee only for a configured fixture province code and method, including
    /// the demo multi-seller surcharge. This is shipping, not the separate ordinary 2%
    /// transaction commission.
    /// </summary>
    public decimal? GetDeliveryFee(AddressSnapshot address, string method, int sellerCount)
    {
        if (!_options.ServiceableProvinceCodes.Contains(
            address.ProvinceCode, StringComparer.Ordinal)
            || method is not ("Standard" or "FreshExpress"))
        {
            return null;
        }

        return (method == "Standard" ? _options.StandardFee : _options.ExpressFee)
            + Math.Max(0, sellerCount - 1) * _options.AdditionalSellerFee;
    }

    /// <summary>
    /// Converts requested fixture points and checks the provisional redemption cap. DB-09
    /// policy/storage is not approved; do not treat these sample rates as production loyalty
    /// rules.
    /// </summary>
    public decimal? GetPointsDiscount(int points, decimal subtotalAfterVoucher)
    {
        decimal discount = points * _options.PointValue;
        return points < 0 || discount > subtotalAfterVoucher * _options.MaxPointsFraction
            ? null : decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
    }

    // A null destination explicitly means UC-RC-03 is not implemented. The frontend
    // displays the order result without inventing a working QR/payment page.
    /// <summary>
    /// Returns the handoff route if the payment module supplies one; null in the demo explicitly
    /// means no QR or gateway has been implemented.
    /// </summary>
    public string? GetPaymentDestination(long orderId) => null;
}
