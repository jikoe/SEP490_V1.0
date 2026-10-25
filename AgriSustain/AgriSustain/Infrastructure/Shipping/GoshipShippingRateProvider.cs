using System.Globalization;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Shipping;

/// <summary>Quotes one parcel per farm through Goship POST /rates.</summary>
public sealed class GoshipShippingRateProvider(GoshipClient client,
    IOptions<GoshipOptions> options) : IShippingRateProvider
{
    private static readonly string[] ExpressServiceKeywords = ["nhanh", "hỏa tốc", "hoả tốc"];

    private readonly GoshipOptions _options = options.Value;

    public int EstimatedDeliveryDays(string deliveryMethod) => deliveryMethod == "Standard"
        ? _options.EstimatedStandardDeliveryDays
        : _options.EstimatedFreshExpressDeliveryDays;

    public async Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            return null;
        }

        _options.PickupLocations.TryGetValue(
            request.FarmerProfileId.ToString(CultureInfo.InvariantCulture), out var pickup);
        if (pickup is null || string.IsNullOrWhiteSpace(pickup.City)
            || string.IsNullOrWhiteSpace(pickup.District)
            || string.IsNullOrWhiteSpace(request.Recipient.ProvinceCode)
            || string.IsNullOrWhiteSpace(request.Recipient.DistrictCode))
        {
            return null;
        }

        var weight = CalculateWeightGrams(request.Lines);
        if (weight is null || weight <= 0)
        {
            return null;
        }

        // Checkout is prepaid (PayOS/VietQR), so no COD amount is collected by the carrier.
        var rates = await client.GetRatesAsync(new GoshipRateRequest(new GoshipShipment(
            new GoshipAddress(pickup.City, pickup.District),
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

    /// <summary>Standard takes the cheapest rate; FreshExpress the cheapest fast ("Nhanh"/"Hỏa tốc") one.</summary>
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
