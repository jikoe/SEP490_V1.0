namespace AgriSustain.Application.DTOs.Checkout;

/// <summary>Commercial values are configured by the owner, never by the browser.</summary>
public sealed class CheckoutPolicy
{
    public int QuoteMinutes { get; set; } = 5;

    public int ReservationMinutes { get; set; } = 15;

    public decimal? PointValueVnd { get; set; }

    public decimal? MaximumPointDiscountPercent { get; set; }

    public DeliveryArea[] Areas { get; set; } = [];
}

