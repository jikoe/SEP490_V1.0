using AgriSustain.Application.DTOs.Products;
using FluentValidation;

namespace AgriSustain.Application.Validators;

public sealed class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    public ProductQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Keyword).MaximumLength(255);
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue);
        RuleFor(x => x.ApprovalStatus).Must(status =>
            status is null or "" or "Pending" or "Approved" or "Rejected");
    }
}

