using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgriSustain.Infrastructure.Shipping;

/// <summary>Typed client for the Goship v2 location and rate APIs.</summary>
public sealed class GoshipClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<IReadOnlyList<GoshipLocation>?> GetCitiesAsync(
        CancellationToken cancellationToken)
    {
        var data = await GetAsync<List<GoshipLocationDto>>("cities", cancellationToken);
        return data?.Select(item => item.ToLocation(null)).ToList();
    }

    public async Task<IReadOnlyList<GoshipLocation>?> GetDistrictsAsync(string cityCode,
        CancellationToken cancellationToken)
    {
        var data = await GetAsync<List<GoshipLocationDto>>(
            $"cities/{Uri.EscapeDataString(cityCode)}/districts", cancellationToken);
        return data?.Select(item => item.ToLocation(item.CityId ?? cityCode)).ToList();
    }

    public async Task<IReadOnlyList<GoshipLocation>?> GetWardsAsync(string districtCode,
        CancellationToken cancellationToken)
    {
        var data = await GetAsync<List<GoshipLocationDto>>(
            $"districts/{Uri.EscapeDataString(districtCode)}/wards", cancellationToken);
        return data?.Select(item => item.ToLocation(item.DistrictId ?? districtCode)).ToList();
    }

    public async Task<IReadOnlyList<GoshipRate>?> GetRatesAsync(GoshipRateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("rates", request, cancellationToken);
            return await ReadAsync<List<GoshipRate>>(response, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return null;
        }
    }

    private async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            return await ReadAsync<T>(response, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return null;
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken) where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var envelope = await response.Content.ReadFromJsonAsync<GoshipEnvelope<T>>(JsonOptions,
            cancellationToken);
        return envelope?.Code == 200 ? envelope.Data : null;
    }

    private static bool IsTransportFailure(Exception exception,
        CancellationToken cancellationToken) => exception switch
    {
        HttpRequestException or JsonException or NotSupportedException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false
    };

    private sealed record GoshipEnvelope<T>(int Code, string? Status, T? Data);

    private sealed record GoshipLocationDto(JsonElement Id, string Name,
        [property: JsonPropertyName("city_id")] string? CityId = null,
        [property: JsonPropertyName("district_id")] string? DistrictId = null)
    {
        // Ward ids are numbers while city/district ids are strings.
        public GoshipLocation ToLocation(string? parentCode) => new(
            Id.ValueKind == JsonValueKind.Number
                ? Id.GetInt64().ToString(CultureInfo.InvariantCulture) : Id.GetString() ?? "",
            Name.Trim(), parentCode);
    }
}

public sealed record GoshipLocation(string Code, string Name, string? ParentCode);

public sealed record GoshipRateRequest(
    [property: JsonPropertyName("shipment")] GoshipShipment Shipment);

public sealed record GoshipShipment(
    [property: JsonPropertyName("address_from")] GoshipAddress AddressFrom,
    [property: JsonPropertyName("address_to")] GoshipAddress AddressTo,
    [property: JsonPropertyName("parcel")] GoshipParcel Parcel);

public sealed record GoshipAddress(
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("district")] string District);

public sealed record GoshipParcel(
    [property: JsonPropertyName("cod")] long Cod,
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("weight")] int Weight);

public sealed record GoshipRate(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("carrier_name")] string? CarrierName,
    [property: JsonPropertyName("carrier_short_name")] string? CarrierShortName,
    [property: JsonPropertyName("service")] string? Service,
    [property: JsonPropertyName("expected")] string? Expected,
    [property: JsonPropertyName("total_fee")] decimal TotalFee);
