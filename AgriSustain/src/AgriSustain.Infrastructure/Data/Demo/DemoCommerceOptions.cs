namespace AgriSustain.Infrastructure.Data.Demo;

public sealed class DemoCommerceOptions
{
    public bool Enabled
    {
        get; set;
    }
    public string[] ServiceableProvinceCodes { get; set; } = [];
    public decimal StandardFee
    {
        get; set;
    }
    public decimal ExpressFee
    {
        get; set;
    }
    public decimal AdditionalSellerFee
    {
        get; set;
    }
    public decimal PointValue
    {
        get; set;
    }
    public decimal MaxPointsFraction
    {
        get; set;
    }
    public int QuoteMinutes
    {
        get; set;
    }
    public int ReservationMinutes
    {
        get; set;
    }
}
