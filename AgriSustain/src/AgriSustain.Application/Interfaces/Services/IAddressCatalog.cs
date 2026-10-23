using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.ValueObjects;

namespace AgriSustain.Application.Interfaces.Services;

public interface IAddressCatalog
{
    /// <summary>
    /// Returns read-only administrative province code/name options from the bundled dataset.
    /// Codes belong to this catalog and are not automatically carrier codes.
    /// </summary>
    IReadOnlyList<ProvinceOption> GetProvinces();
    /// <summary>
    /// Returns read-only ward options whose parent_code matches the requested province,
    /// preventing the UI from offering an unrelated ward.
    /// </summary>
    IReadOnlyList<WardOption> GetWards(string provinceCode);
    /// <summary>
    /// Validates the code pair and replaces untrusted client names/version with the catalog
    /// snapshot. Null means invalid or mismatched administrative codes; carrier serviceability is
    /// checked separately.
    /// </summary>
    AddressSnapshot? Normalize(AddressSnapshot address);
}
