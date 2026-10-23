using AgriSustain.Application.DTOs.Administration;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

public sealed class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    /// <summary>
    /// Defines approved category input bounds: trimmed name length, description length,
    /// nonnegative priority and positive optional parent ID. No image, uniqueness or Version rule
    /// is registered.
    /// </summary>
    public SaveCategoryValidator()
    {
        RuleFor(request => request.Name).NotEmpty()
            .Must(name => name != null && name.Trim().Length is >= 2 and <= 100)
            .WithMessage("Tên danh mục phải có 2–100 ký tự.");
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.Priority).GreaterThanOrEqualTo(0);
        RuleFor(request => request.ParentId).GreaterThan(0)
            .When(request => request.ParentId != null);
    }
}
