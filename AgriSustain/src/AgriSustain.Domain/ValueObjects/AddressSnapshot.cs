namespace AgriSustain.Domain.ValueObjects;

public sealed record AddressSnapshot(
    string RecipientName, string Phone, string Province, string Ward, string Detail,
    string ProvinceCode, string WardCode, string CatalogVersion = "");
