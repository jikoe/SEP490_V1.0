using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Common;
using FluentValidation;

namespace AgriSustain.Application.Validators;

public sealed class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000).WithMessage(ProductListingMessages.Field("trang", "giá trị từ 1 đến 100000"));
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(ProductListingMessages.Field("số dòng mỗi trang", "giá trị từ 1 đến 100"));
        RuleFor(x => x.Keyword).MaximumLength(255).WithMessage(ProductListingMessages.Field("từ khóa", "tối đa 255 ký tự"));
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage(ProductListingMessages.Field("danh mục", "chọn danh mục hợp lệ")).When(x => x.CategoryId.HasValue);
        RuleFor(x => x.ApprovalStatus).Must(status =>
            status is null or "" or "Pending" or "Approved" or "Rejected")
            .WithMessage(ProductListingMessages.Field("trạng thái", "chọn trạng thái kiểm duyệt hợp lệ"));
    }
}

