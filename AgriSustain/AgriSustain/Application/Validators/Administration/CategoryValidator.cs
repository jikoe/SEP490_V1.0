using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class CategoryValidator : AbstractValidator<CategoryRequest>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId.HasValue);
        RuleFor(x => x.ImageBase64).MaximumLength(7_000_000);
        RuleFor(x => x).Must(x => !x.RemoveImage || x.ImageBase64 is null);
    }
}
