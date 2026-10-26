namespace AgriSustain.Application.DTOs.Checkout;

public sealed record CheckoutAddress(string RecipientName, string Phone, string ProvinceCode,
    string Province, string DistrictCode, string District, string WardCode, string Ward,
    string Detail, string CatalogVersion = "");
