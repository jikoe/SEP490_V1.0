namespace AgriSustain.Application.DTOs.Checkout;

public sealed record DeliveryArea(string ProvinceCode, string Province, string District,
    string WardCode, string Ward, decimal StandardFee, decimal ExpressFee,
    int StandardDays, int ExpressDays);
