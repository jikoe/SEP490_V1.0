using AgriSustain.Application.DTOs.Products;
using FluentValidation;

namespace AgriSustain.Application.Validators;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OriginProvince).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(10000);
        RuleFor(x => x.BaseRetailPrice).GreaterThan(0).LessThanOrEqualTo(999999999999m)
            .PrecisionScale(18, 2, true);
        RuleFor(x => x.WholesaleMinQuantity).GreaterThan(1)
            .LessThanOrEqualTo(999999999m)
            .Must(value => !value.HasValue || decimal.Round(value.Value, 3) == value.Value)
            .When(x => x.WholesaleMinQuantity.HasValue);
        RuleFor(x => x.WholesalePrice).GreaterThan(0)
            .LessThanOrEqualTo(x => x.BaseRetailPrice)
            .Must(value => !value.HasValue || decimal.Round(value.Value, 2) == value.Value)
            .When(x => x.WholesalePrice.HasValue);
        RuleFor(x => x).Must(x => x.WholesalePrice.HasValue
            == x.WholesaleMinQuantity.HasValue).WithMessage("Nhập đủ giá và ngưỡng bán sỉ.");
        RuleFor(x => x.Images).NotNull().Must(images => images is { Count: > 0 and <= 10 }
            && images.All(image => image is not null)
            && images.Select(image => image.ImageUrl).Distinct().Count() == images.Count
            && images.Count(image => image.IsPrimary) == 1)
            .WithMessage("Chọn 1–10 ảnh và đúng một ảnh đại diện (MSG76).");
        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(500)
                .Must(IsImageReference);
            image.RuleFor(x => x.AltText).MaximumLength(255);
        });
    }

    private static bool IsImageReference(string? value) => value is not null
        && (value.StartsWith("/media/", StringComparison.Ordinal)
            || Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps) && !value.Contains("..");
}


