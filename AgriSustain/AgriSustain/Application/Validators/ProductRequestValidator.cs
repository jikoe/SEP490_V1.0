using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Common;
using FluentValidation;

namespace AgriSustain.Application.Validators;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty().WithMessage(ProductListingMessages.Field("tên sản phẩm", "không được để trống"))
            .MaximumLength(255).WithMessage(ProductListingMessages.Field("tên sản phẩm", "tối đa 255 ký tự"));
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage(ProductListingMessages.Field("danh mục", "chọn danh mục đang hoạt động"));
        RuleFor(x => x.Unit).NotEmpty().WithMessage(ProductListingMessages.Field("đơn vị", "không được để trống"))
            .MaximumLength(50).WithMessage(ProductListingMessages.Field("đơn vị", "tối đa 50 ký tự"));
        RuleFor(x => x.Description).NotEmpty().WithMessage(ProductListingMessages.Field("mô tả", "không được để trống"))
            .MaximumLength(10000).WithMessage(ProductListingMessages.Field("mô tả", "tối đa 10000 ký tự"));
        RuleFor(x => x.BaseRetailPrice).GreaterThan(0).WithMessage(ProductListingMessages.MSG77).LessThanOrEqualTo(999999999999m).WithMessage(ProductListingMessages.MSG77)
            .PrecisionScale(18, 2, true).WithMessage(ProductListingMessages.MSG77);
        RuleFor(x => x.WholesaleMinQuantity).GreaterThan(1).WithMessage(ProductListingMessages.MSG77)
            .LessThanOrEqualTo(999999999m).WithMessage(ProductListingMessages.MSG77)
            .Must(value => !value.HasValue || decimal.Round(value.Value, 3) == value.Value).WithMessage(ProductListingMessages.MSG77)
            .When(x => x.WholesaleMinQuantity.HasValue);
        RuleFor(x => x.WholesalePrice).GreaterThan(0).WithMessage(ProductListingMessages.MSG77)
            .LessThanOrEqualTo(x => x.BaseRetailPrice).WithMessage(ProductListingMessages.MSG77)
            .Must(value => !value.HasValue || decimal.Round(value.Value, 2) == value.Value).WithMessage(ProductListingMessages.MSG77)
            .When(x => x.WholesalePrice.HasValue);
        RuleFor(x => x).Must(x => x.WholesalePrice.HasValue
            == x.WholesaleMinQuantity.HasValue).WithMessage(ProductListingMessages.MSG77);
        RuleFor(x => x.Images).NotNull().WithMessage(ProductListingMessages.MSG76).Must(images => images is { Count: > 0 and <= 10 }
            && images.All(image => image is not null)
            && images.Select(image => image.ImageUrl).Distinct().Count() == images.Count
            && images.Count(image => image.IsPrimary) == 1)
            .WithMessage(ProductListingMessages.MSG76);
        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(x => x.ImageUrl).NotEmpty().WithMessage(ProductListingMessages.MSG78).MaximumLength(500).WithMessage(ProductListingMessages.MSG78)
                .Must(IsImageReference).WithMessage(ProductListingMessages.MSG78);
            image.RuleFor(x => x.AltText).MaximumLength(255).WithMessage(ProductListingMessages.Field("mô tả ảnh", "tối đa 255 ký tự"));
        });
    }

    private static bool IsImageReference(string? value) => value is not null
        && (value.StartsWith("/media/", StringComparison.Ordinal)
            || Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps) && !value.Contains("..");
}


