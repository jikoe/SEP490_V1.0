using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class CategoryQueryValidator : AbstractValidator<CategoryQuery>
{
    /// <summary>
    /// Bounds category search text, pagination and optional parent filters before any query
    /// executes.
    /// </summary>
    public CategoryQueryValidator()
    {
        RuleFor(query => query.Keyword).MaximumLength(100);
        RuleFor(query => query.Id).GreaterThan(0).When(query => query.Id != null);
        RuleFor(query => query.Page).InclusiveBetween(1, 100000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.ParentId).GreaterThan(0).When(query => query.ParentId != null);
        RuleFor(query => query.RootOnly).Equal(false).When(query => query.ParentId != null);
    }
}
