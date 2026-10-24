using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Shipping;

public sealed class GhtkShippingRateProvider(HttpClient httpClient,
    IOptions<GhtkOptions> options) : IGhtkShippingRateProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GhtkOptions _options = options.Value;

    public int EstimatedDeliveryDays(string deliveryMethod) => deliveryMethod == "Standard"
        ? _options.EstimatedStandardDeliveryDays
        : _options.EstimatedFreshExpressDeliveryDays;

    public async Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Token)
            || string.IsNullOrWhiteSpace(_options.ClientSource))
        {
            return null;
        }

        _options.PickupLocations.TryGetValue(
            request.FarmerProfileId.ToString(CultureInfo.InvariantCulture), out var pickup);
        if (pickup is null || !HasPickupAddress(pickup))
        {
            return null;
        }

        var weight = CalculateWeightGrams(request.Lines);
        if (weight is null || weight <= 0)
        {
            return null;
        }

        var query = new Dictionary<string, string?>
        {
            ["address"] = request.Recipient.Detail,
            ["province"] = request.Recipient.Province,
            ["ward"] = request.Recipient.Ward,
            ["weight"] = weight.Value.ToString(CultureInfo.InvariantCulture),
            ["value"] = decimal.ToInt64(decimal.Ceiling(request.DeclaredValue))
                .ToString(CultureInfo.InvariantCulture),
            ["transport"] = request.DeliveryMethod == "Standard" ? "road" : "fly"
        };

        if (!string.IsNullOrWhiteSpace(pickup.PickAddressId))
        {
            query["pick_address_id"] = pickup.PickAddressId;
        }
        else
        {
            query["pick_address"] = pickup.Address;
            query["pick_province"] = pickup.Province;
            query["pick_ward"] = pickup.Ward;
            query["pick_district"] = pickup.District;
        }

        var uri = "services/shipment/fee?" + string.Join("&", query
            .Where(pair => pair.Value is not null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.TryAddWithoutValidation("Token", _options.Token);
        message.Headers.TryAddWithoutValidation("X-Client-Source", _options.ClientSource);

        GhtkFeeResponse? result;
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            result = await response.Content.ReadFromJsonAsync<GhtkFeeResponse>(JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (result?.Success != true || result.Fee is null || result.Fee.Fee < 0)
        {
            return null;
        }

        var additionalFees = (result.Fee.ExtFees ?? [])
            .Sum(item => Math.Max(0, item.Amount));
        return new ShippingRateResult(result.Fee.Delivery,
            result.Fee.Fee + Math.Max(0, result.Fee.InsuranceFee) + additionalFees);
    }

    private static bool HasPickupAddress(GhtkPickupLocationOptions pickup) =>
        !string.IsNullOrWhiteSpace(pickup.PickAddressId)
        || !string.IsNullOrWhiteSpace(pickup.Address)
            && !string.IsNullOrWhiteSpace(pickup.Province)
            && !string.IsNullOrWhiteSpace(pickup.Ward);

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

    private sealed record GhtkFeeResponse(bool Success, GhtkFee? Fee);

    private sealed record GhtkFee(bool Delivery, decimal Fee, decimal InsuranceFee = 0,
        IReadOnlyList<GhtkAdditionalFee>? ExtFees = null);

    private sealed record GhtkAdditionalFee(decimal Amount);
}
