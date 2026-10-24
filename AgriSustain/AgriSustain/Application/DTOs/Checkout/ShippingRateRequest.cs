namespace AgriSustain.Application.DTOs.Checkout;

public sealed record ShippingPackageLine(string Unit, decimal Quantity);

public sealed record ShippingRateRequest(long FarmerProfileId, CheckoutAddress Recipient,
    IReadOnlyList<ShippingPackageLine> Lines, decimal DeclaredValue, string DeliveryMethod);

public sealed record ShippingRateResult(bool IsSupported, decimal? Fee);
