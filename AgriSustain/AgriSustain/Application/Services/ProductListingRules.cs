using AgriSustain.Application.DTOs.Products;
using AgriSustain.Domain.Entities;
namespace AgriSustain.Application.Services;

public sealed class ProductListingRules
{
    public Task<FluentValidation.Results.ValidationResult> ValidateSubmission(
        ProductRequest input, FluentValidation.IValidator<ProductRequest> validator, CancellationToken ct) =>
        validator.ValidateAsync(input, ct);
    public bool EnsureUnitUnchanged(Product product, ProductRequest input, bool unitLocked) =>
        !unitLocked || product.Unit == input.Unit.Trim();
    public bool HasContentChanged(Product product, ProductRequest input) =>
        product.ProductName != input.ProductName.Trim() || product.CategoryId != input.CategoryId
        || product.FarmCertificateId != input.FarmCertificateId
        || product.Unit != input.Unit.Trim() || (product.Description ?? "") != input.Description.Trim()
        || !product.ProductImages.OrderBy(x => x.DisplayOrder)
            .Select(x => new ProductImageInput(x.ImageUrl, x.IsPrimary, x.AltText))
            .SequenceEqual(input.Images);
    public bool ValidatePriceChange(Product product, ProductRequest input) =>
        !HasContentChanged(product, input);
    public bool ValidateCertificate(FarmCertificate? certificate, long farmerId, DateOnly today) =>
        certificate is not null && certificate.FarmerProfileId == farmerId
        && MarketplaceRules.ValidCertificate(today).Compile()(certificate);
    public bool ValidateVisibility(Product product, bool published) =>
        !published || product.ApprovalStatus == "Approved";
}
