using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

/// <summary>BR-CAT-02: MSG02 for a missing name, MSG08 for a value over its maximum length.</summary>
public sealed class CategoryValidator : AbstractValidator<CategoryRequest>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Trường Tên danh mục là bắt buộc.")
            .Must(name => name is null || name.Trim().Length <= Category.NameMaxLength)
            .WithMessage($"Vượt quá độ dài tối đa {Category.NameMaxLength} ký tự.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("Vượt quá độ dài tối đa 500 ký tự.");
    }
}
