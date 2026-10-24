namespace AgriSustain.Infrastructure.Shipping;

public sealed class GhtkOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://services.giaohangtietkiem.vn/";

    public string? Token { get; set; }

    public string? ClientSource { get; set; }

    public int EstimatedStandardDeliveryDays { get; set; } = 3;

    public int EstimatedFreshExpressDeliveryDays { get; set; } = 1;

    public Dictionary<string, GhtkPickupLocationOptions> PickupLocations { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class GhtkPickupLocationOptions
{
    public string? PickAddressId { get; set; }

    public string? Address { get; set; }

    public string? Province { get; set; }

    public string? Ward { get; set; }

    public string? District { get; set; }
}
