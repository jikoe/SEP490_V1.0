namespace AgriSustain.Domain.Entities;

/// <summary>
/// UC-RC-02 Step 5: the farm's Goship pickup address. Codes come from the same Goship
/// province/district/ward catalog as checkout (Database/20261107_farmer_pickup_address.sql).
/// </summary>
public partial class FarmerProfile
{
    public string? PickupProvinceCode { get; set; }

    public string? PickupDistrictCode { get; set; }

    public string? PickupWardCode { get; set; }

    /// <summary>Catalog names of the codes, e.g. "Phường X, Quận Y, Hà Nội", for display.</summary>
    public string? PickupAddressText { get; set; }

    public bool HasPickupAddress => !string.IsNullOrWhiteSpace(PickupProvinceCode)
        && !string.IsNullOrWhiteSpace(PickupDistrictCode);
}
