using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

/// <summary>Three-level address catalog (province, district, ward) used by checkout.</summary>
public interface IAddressCatalog
{
    /// <summary>
    /// Provinces of the catalogue.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Provinces; empty when the catalogue is unavailable.</returns>
    Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Districts of a province.
    /// </summary>
    /// <param name="provinceCode">Province code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Districts.</returns>
    Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Wards of a district.
    /// </summary>
    /// <param name="districtCode">District code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Wards.</returns>
    Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a trusted snapshot, or null for invalid province/district/ward codes.
    /// </summary>
    /// <param name="address">Address from the browser.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Address with catalogue names and version, or null.</returns>
    Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address,
        CancellationToken cancellationToken);
}
