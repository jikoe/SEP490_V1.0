using System.Security.Cryptography;
using System.Text.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Infrastructure.Data.Geography;

// Source: adminvsrm/vndata_hanhchinh; the bundled LICENSE applies to these data files.
// This is a real administrative catalog, not proof that a carrier serves an address.
public sealed class VietnamAddressCatalog : IAddressCatalog
{
    private readonly IReadOnlyList<ProvinceOption> _provinces;
    private readonly IReadOnlyList<WardOption> _wards;
    private readonly string _version;

    /// <summary>
    /// Loads the pinned local province/ward JSON objects and derives a SHA-256 dataset version.
    /// Fails on unreadable/malformed required data instead of silently trusting client address
    /// names.
    /// </summary>
    public VietnamAddressCatalog()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Data", "Geography");
        byte[] provinceBytes = File.ReadAllBytes(Path.Combine(directory, "province.json"));
        byte[] wardBytes = File.ReadAllBytes(Path.Combine(directory, "ward.json"));
        using var provinces = JsonDocument.Parse(provinceBytes);
        using var wards = JsonDocument.Parse(wardBytes);
        _provinces = Array.AsReadOnly(provinces.RootElement.EnumerateObject().Select(item =>
            new ProvinceOption(Read(item.Value, "code"),
                Read(item.Value, "name_with_type"))).ToArray());
        _wards = Array.AsReadOnly(wards.RootElement.EnumerateObject().Select(item =>
            new WardOption(Read(item.Value, "code"), Read(item.Value, "parent_code"),
                Read(item.Value, "name_with_type"))).ToArray());
        _version = Convert.ToHexString(SHA256.HashData(provinceBytes.Concat(wardBytes).ToArray()));
    }

    /// <summary>
    /// Returns read-only administrative province code/name options from the bundled dataset.
    /// Codes belong to this catalog and are not automatically carrier codes.
    /// </summary>
    public IReadOnlyList<ProvinceOption> GetProvinces() => _provinces;

    /// <summary>
    /// Returns read-only ward options whose parent_code matches the requested province,
    /// preventing the UI from offering an unrelated ward.
    /// </summary>
    public IReadOnlyList<WardOption> GetWards(string provinceCode) => Array.AsReadOnly(
        _wards.Where(ward => ward.ProvinceCode == provinceCode).ToArray());

    /// <summary>
    /// Validates the code pair and replaces untrusted client names/version with the catalog
    /// snapshot. Null means invalid or mismatched administrative codes; carrier serviceability is
    /// checked separately.
    /// </summary>
    public AddressSnapshot? Normalize(AddressSnapshot address)
    {
        ProvinceOption? province = _provinces.SingleOrDefault(p => p.Code == address.ProvinceCode);
        WardOption? ward = _wards.SingleOrDefault(w => w.Code == address.WardCode
            && w.ProvinceCode == address.ProvinceCode);
        return province == null || ward == null ? null : address with
        {
            Province = province.Name,
            Ward = ward.Name,
            CatalogVersion = _version
        };
    }

    /// <summary>
    /// Requires a nonnull string field in a bundled catalog entry. Missing or invalid data is a
    /// startup error, not an empty geographic identifier.
    /// </summary>
    private static string Read(JsonElement item, string name) =>
        item.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Missing geographic catalog field: {name}");
}
