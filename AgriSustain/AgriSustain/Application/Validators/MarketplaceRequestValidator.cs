using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Validators;

public static class MarketplaceRequestValidator
{
    public static bool IsValid(BrowseMarketplaceRequest request) =>
        request.Page is > 0 and <= 1000000
        && request.PageSize is > 0 and <= 100
        && request.CategoryId is null or > 0
        && request.SupplierId is null or > 0
        && request.CropSeasonId is null or > 0
        && request.MinPrice is null or >= 0 and <= 9999999999999999.99m
        && request.MaxPrice is null or >= 0 and <= 9999999999999999.99m
        && !(request.MinPrice > request.MaxPrice)
        && (request.MinRating == null
            || (double.IsFinite(request.MinRating.Value) && request.MinRating is >= 0 and <= 5))
        && IsTextValid(request.Keyword, 150)
        && IsTextValid(request.Region, 100)
        && request.Certifications.Length <= 20
        && request.Certifications.All(value => !string.IsNullOrWhiteSpace(value)
            && IsTextValid(value, 100))
        && request.PriceRanges.Length <= 4
        && request.PriceRanges.All(value => value is "under50" or "50to100"
            or "100to200" or "over200")
        && request.Sort is "newest" or "price-asc" or "price-desc" or "rating";

    private static bool IsTextValid(string? value, int maximumLength) =>
        value == null || (value.Length <= maximumLength && !value.Any(char.IsControl));
}
