using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Shipping;

/// <summary>Uses Goship's own city/district/ward codes so quotes can be sent to the rate API as-is.</summary>
public sealed class GoshipAddressCatalog(GoshipClient client, IMemoryCache cache,
    IOptions<GoshipOptions> options) : IAddressCatalog
{
    /// <summary>Stable version so a quote snapshot still equals the address re-normalized at order time.</summary>
    public const string Version = "goship-v2";

    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(
        Math.Max(1, options.Value.CatalogCacheHours));

    public async Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(
        CancellationToken cancellationToken)
    {
        var cities = await CachedAsync("goship:cities",
            () => client.GetCitiesAsync(cancellationToken));
        return cities.Select(city => new ProvinceOption(city.Code, city.Name)).ToList();
    }

    public async Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return [];
        }
        var districts = await CachedAsync($"goship:districts:{provinceCode}",
            () => client.GetDistrictsAsync(provinceCode, cancellationToken));
        return districts.Select(district => new DistrictOption(district.Code,
            district.ParentCode ?? provinceCode, district.Name)).ToList();
    }

    public async Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(districtCode))
        {
            return [];
        }
        var wards = await CachedAsync($"goship:wards:{districtCode}",
            () => client.GetWardsAsync(districtCode, cancellationToken));
        return wards.Select(ward => new WardOption(ward.Code,
            ward.ParentCode ?? districtCode, ward.Name)).ToList();
    }

    public async Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(address.ProvinceCode) || string.IsNullOrEmpty(address.DistrictCode)
            || string.IsNullOrEmpty(address.WardCode))
        {
            return null;
        }
        var province = (await GetProvincesAsync(cancellationToken))
            .FirstOrDefault(item => item.Code == address.ProvinceCode);
        var district = province is null ? null
            : (await GetDistrictsAsync(province.Code, cancellationToken))
                .FirstOrDefault(item => item.Code == address.DistrictCode);
        var ward = district is null ? null
            : (await GetWardsAsync(district.Code, cancellationToken))
                .FirstOrDefault(item => item.Code == address.WardCode);
        return province is null || district is null || ward is null ? null : address with
        {
            Province = province.Name,
            District = district.Name,
            Ward = ward.Name,
            CatalogVersion = Version
        };
    }

    // Failed or empty responses are not cached so an outage does not hide the catalog.
    private async Task<IReadOnlyList<GoshipLocation>> CachedAsync(string key,
        Func<Task<IReadOnlyList<GoshipLocation>?>> load)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<GoshipLocation>? cached) && cached is not null)
        {
            return cached;
        }
        var items = await load();
        if (items is null || items.Count == 0)
        {
            return [];
        }
        cache.Set(key, items, _cacheDuration);
        return items;
    }
}
