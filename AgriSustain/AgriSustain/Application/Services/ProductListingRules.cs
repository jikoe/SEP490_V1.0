using AgriSustain.Application.DTOs.Products;
using AgriSustain.Domain.Entities;
namespace AgriSustain.Application.Services;

public sealed class ProductListingRules
{
    public bool HasContentChanged(Product product, ProductRequest input) =>
        product.ProductName != input.ProductName.Trim() || product.CategoryId != input.CategoryId
        || product.Unit != input.Unit.Trim() || (product.Description ?? "") != input.Description.Trim()
        || !product.ProductImages.OrderBy(x => x.DisplayOrder)
            .Select(x => new ProductImageInput(x.ImageUrl, x.IsPrimary, x.AltText))
            .SequenceEqual(input.Images);
    public bool ValidatePriceChange(Product product, ProductRequest input) =>
        !HasContentChanged(product, input);
    public bool ValidateVisibility(Product product, bool published) =>
        !published || product.ApprovalStatus == "Approved";
}
