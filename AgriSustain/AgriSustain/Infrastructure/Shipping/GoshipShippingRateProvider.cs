using System.Globalization;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Shipping;

/// <summary>
/// Quotes one parcel per farm through Goship POST /rates. A null result is shown to the customer
/// as MSG-CO-02; the cause is logged for the team instead.
/// </summary>
public sealed class GoshipShippingRateProvider(GoshipClient client,
    IOptions<GoshipOptions> options, ILogger<GoshipShippingRateProvider>? logger = null)
    : IShippingRateProvider
{
    private readonly ILogger _logger = logger ?? NullLogger<GoshipShippingRateProvider>.Instance;

    private static readonly string[] ExpressServiceKeywords = ["nhanh", "hỏa tốc", "hoả tốc"];

    private readonly GoshipOptions _options = options.Value;

    /// <summary>
    /// Configured days: Standard or FreshExpress.
    /// </summary>
    /// <param name="deliveryMethod">Method.</param>
    /// <returns>Days.</returns>
    public int EstimatedDeliveryDays(string deliveryMethod) => deliveryMethod == "Standard"
        ? _options.EstimatedStandardDeliveryDays
        : _options.EstimatedFreshExpressDeliveryDays;

    /// <summary>
    /// Returns null (shown as MSG-CO-02, cause logged) when Goship is disabled or has no token, the farm has no pickup
    /// address (profile, else configured fallback), the recipient has no province/district, or the weight cannot be
    /// derived from the units. Otherwise calls Goship POST /rates for one parcel (no COD because checkout is prepaid) and
    /// picks the cheapest rate for the method.
    /// </summary>
    /// <param name="request">Farm, recipient, parcel lines, declared value, method and pickup.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Fee when a rate exists, IsSupported false when no rate fits the method, or null.</returns>
    public async Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            _logger.LogWarning("Goship is disabled or has no access token; no delivery fee was quoted.");
            return null;
        }

        // UC-RC-02 Step 5: the pickup address is the farm's Goship address saved in its Farmer profile.
        // Goship__PickupLocations__{FarmerProfileId} is only a fallback for farms that have not saved one yet.
        string? pickupCity = request.Pickup?.ProvinceCode;
        string? pickupDistrict = request.Pickup?.DistrictCode;
        if ((string.IsNullOrWhiteSpace(pickupCity) || string.IsNullOrWhiteSpace(pickupDistrict))
            && _options.PickupLocations.TryGetValue(
                request.FarmerProfileId.ToString(CultureInfo.InvariantCulture), out var configured))
        {
            pickupCity = configured.City;
            pickupDistrict = configured.District;
        }
        if (string.IsNullOrWhiteSpace(pickupCity) || string.IsNullOrWhiteSpace(pickupDistrict))
        {
            _logger.LogWarning("Farmer profile {FarmerProfileId} has no Goship pickup address.",
                request.FarmerProfileId);
            return null;
        }
        if (string.IsNullOrWhiteSpace(request.Recipient.ProvinceCode)
            || string.IsNullOrWhiteSpace(request.Recipient.DistrictCode))
        {
            return null;
        }

        var weight = CalculateWeightGrams(request.Lines);
        if (weight is null || weight <= 0)
        {
            _logger.LogWarning("Parcel weight for farmer profile {FarmerProfileId} could not be derived from the selling units.",
                request.FarmerProfileId);
            return null;
        }

        // Checkout is prepaid (SePay), so no COD amount is collected by the carrier.
        var rates = await client.GetRatesAsync(new GoshipRateRequest(new GoshipShipment(
            new GoshipAddress(pickupCity, pickupDistrict),
            new GoshipAddress(request.Recipient.ProvinceCode, request.Recipient.DistrictCode),
            new GoshipParcel(0, decimal.ToInt64(decimal.Ceiling(request.DeclaredValue)),
                _options.ParcelWidth, _options.ParcelHeight, _options.ParcelLength,
                weight.Value))), cancellationToken);
        if (rates is null)
        {
            return null;
        }

        var selected = SelectRate(rates, request.DeliveryMethod);
        return selected is null
            ? new ShippingRateResult(false, null)
            : new ShippingRateResult(true, selected.TotalFee);
    }

    /// <summary>
    /// Standard takes the cheapest rate; FreshExpress the cheapest fast ("Nhanh"/"Hỏa tốc") one.
    /// </summary>
    /// <param name="rates">Rates from Goship.</param>
    /// <param name="deliveryMethod">Method.</param>
    /// <returns>Chosen rate or null.</returns>
    private static GoshipRate? SelectRate(IEnumerable<GoshipRate> rates, string deliveryMethod)
    {
        var candidates = rates.Where(rate => rate.TotalFee >= 0);
        if (deliveryMethod != "Standard")
        {
            candidates = candidates.Where(rate => rate.Service is not null
                && ExpressServiceKeywords.Any(keyword => rate.Service.Normalize().Contains(
                    keyword, StringComparison.OrdinalIgnoreCase)));
        }
        return candidates.OrderBy(rate => rate.TotalFee).FirstOrDefault();
    }

    /// <summary>
    /// Parcel weight from the selling units: kg and g only; any other unit makes the weight unknown.
    /// </summary>
    /// <param name="lines">Unit and quantity of each line.</param>
    /// <returns>Grams, or null.</returns>
    private static int? CalculateWeightGrams(IReadOnlyList<ShippingPackageLine> lines)
    {
        decimal totalGrams = 0;
        foreach (var line in lines)
        {
            var gramsPerUnit = line.Unit.Trim().ToLowerInvariant() switch
            {
                "kg" or "kilogram" or "kilograms" => 1000m,
                "g" or "gram" or "grams" => 1m,
                _ => 0m
            };
            if (gramsPerUnit == 0)
            {
                return null;
            }
            totalGrams += line.Quantity * gramsPerUnit;
        }

        return totalGrams <= 0 || totalGrams > int.MaxValue
            ? null : decimal.ToInt32(decimal.Ceiling(totalGrams));
    }
}
