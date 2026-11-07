namespace AgriSustain.Application.DTOs.Checkout;

public sealed record ShippingPackageLine(string Unit, decimal Quantity);

/// <summary>Pickup is the farm's Goship address from its Farmer profile (UC-RC-02 Step 5).</summary>
public sealed record ShippingRateRequest(long FarmerProfileId, CheckoutAddress Recipient,
    IReadOnlyList<ShippingPackageLine> Lines, decimal DeclaredValue, string DeliveryMethod,
    ShippingPickup? Pickup = null);

public sealed record ShippingPickup(string ProvinceCode, string DistrictCode);

public sealed record ShippingRateResult(bool IsSupported, decimal? Fee);
