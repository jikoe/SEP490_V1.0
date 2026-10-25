using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

/// <summary>Three-level address catalog (province, district, ward) used by checkout.</summary>
public interface IAddressCatalog
{
    Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode,
        CancellationToken cancellationToken);

    /// <summary>Returns a trusted snapshot, or null for invalid province/district/ward codes.</summary>
    Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address,
        CancellationToken cancellationToken);
}
