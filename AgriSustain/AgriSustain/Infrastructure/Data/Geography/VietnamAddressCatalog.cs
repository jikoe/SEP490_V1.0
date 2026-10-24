using System.Security.Cryptography;
using System.Text.Json;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Infrastructure.Data.Geography;

/// <summary>Reads the bundled administrative catalog independently of delivery coverage.</summary>
public sealed class VietnamAddressCatalog : IAddressCatalog
{
    private readonly IReadOnlyList<ProvinceOption> _provinces;
    private readonly IReadOnlyDictionary<string, ProvinceOption> _provincesByCode;
    private readonly IReadOnlyDictionary<string, WardOption> _wardsByCode;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<WardOption>> _wardsByProvince;
    private readonly string _version;

    public VietnamAddressCatalog()
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory, "Infrastructure", "Data", "Geography");
        var provinceBytes = File.ReadAllBytes(Path.Combine(directory, "province.json"));
        var wardBytes = File.ReadAllBytes(Path.Combine(directory, "ward.json"));
        using var provinceDocument = JsonDocument.Parse(provinceBytes);
        using var wardDocument = JsonDocument.Parse(wardBytes);
        _provinces = Array.AsReadOnly(provinceDocument.RootElement.EnumerateObject()
            .Select(item => new ProvinceOption(
                Read(item.Value, "code"), Read(item.Value, "name_with_type")))
            .ToArray());
        var wards = wardDocument.RootElement.EnumerateObject()
            .Select(item => new WardOption(
                Read(item.Value, "code"),
                Read(item.Value, "parent_code"),
                Read(item.Value, "name_with_type")))
            .ToArray();
        _provincesByCode = _provinces.ToDictionary(item => item.Code, StringComparer.Ordinal);
        _wardsByCode = wards.ToDictionary(item => item.Code, StringComparer.Ordinal);
        if (_provinces.Count == 0
            || wards.Length == 0
            || wards.Any(ward => !_provincesByCode.ContainsKey(ward.ProvinceCode)))
        {
            throw new InvalidDataException("Invalid bundled province/ward catalog.");
        }
        _wardsByProvince = wards.GroupBy(ward => ward.ProvinceCode)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<WardOption>)Array.AsReadOnly(group.ToArray()),
                StringComparer.Ordinal);
        _version = Convert.ToHexString(
            SHA256.HashData(provinceBytes.Concat(wardBytes).ToArray()));
    }

    public IReadOnlyList<ProvinceOption> GetProvinces() => _provinces;

    public IReadOnlyList<WardOption> GetWards(string provinceCode)
    {
        return _wardsByProvince.TryGetValue(provinceCode, out var wards)
            ? wards : Array.Empty<WardOption>();
    }

    public CheckoutAddress? Normalize(CheckoutAddress address)
    {
        if (string.IsNullOrEmpty(address.ProvinceCode)
            || string.IsNullOrEmpty(address.WardCode)
            || !_provincesByCode.TryGetValue(address.ProvinceCode, out var province)
            || !_wardsByCode.TryGetValue(address.WardCode, out var ward)
            || ward.ProvinceCode != province.Code)
        {
            return null;
        }
        return address with
        {
            Province = province.Name,
            District = "",
            Ward = ward.Name,
            CatalogVersion = _version
        };
    }

    private static string Read(JsonElement item, string name)
    {
        var value = item.GetProperty(name).GetString();
        return !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidDataException($"Missing geographic catalog field: {name}");
    }
}
