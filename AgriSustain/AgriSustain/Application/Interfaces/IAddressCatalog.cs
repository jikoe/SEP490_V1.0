using AgriSustain.Application.DTOs.Checkout;

namespace AgriSustain.Application.Interfaces;

public interface IAddressCatalog
{
    IReadOnlyList<ProvinceOption> GetProvinces();

    IReadOnlyList<WardOption> GetWards(string provinceCode);

    /// <summary>Returns a trusted snapshot, or null for invalid province/ward codes.</summary>
    CheckoutAddress? Normalize(CheckoutAddress address);
}
