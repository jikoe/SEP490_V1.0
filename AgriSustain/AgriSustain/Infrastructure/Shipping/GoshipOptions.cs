namespace AgriSustain.Infrastructure.Shipping;

public sealed class GoshipOptions
{
    public bool Enabled { get; set; }

    /// <summary>Sandbox: https://sandbox.goship.io/api/v2/, production: https://api.goship.io/api/v2/.</summary>
    public string BaseUrl { get; set; } = "https://sandbox.goship.io/api/v2/";

    /// <summary>Bearer token returned by POST /login; configure through a secret store.</summary>
    public string? AccessToken { get; set; }

    public int EstimatedStandardDeliveryDays { get; set; } = 3;

    public int EstimatedFreshExpressDeliveryDays { get; set; } = 1;

    /// <summary>Default parcel size in centimetres sent with every rate request.</summary>
    public int ParcelLength { get; set; } = 30;

    public int ParcelWidth { get; set; } = 20;

    public int ParcelHeight { get; set; } = 20;

    public int CatalogCacheHours { get; set; } = 24;

    public Dictionary<string, GoshipPickupLocationOptions> PickupLocations { get; set; } =
        new(StringComparer.Ordinal);
}

/// <summary>Goship city/district codes of a farm's pickup address.</summary>
public sealed class GoshipPickupLocationOptions
{
    public string? City { get; set; }

    public string? District { get; set; }
}
