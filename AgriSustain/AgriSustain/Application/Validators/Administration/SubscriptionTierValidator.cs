using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;
using FluentValidation;

namespace AgriSustain.Application.Validators.Administration;

/// <summary>BR-ST-01: MSG02 for a missing value, MSG08 for a value over its maximum length.</summary>
public sealed class SubscriptionTierValidator : AbstractValidator<SubscriptionTierRequest>
{
    public SubscriptionTierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Trường Tên gói là bắt buộc.")
            .Must(name => name is null || name.Trim().Length > 0).WithMessage("Trường Tên gói là bắt buộc.")
            .Must(name => name is null || name.Trim().Length <= B2bsubscriptionPackage.NameMaxLength)
            .WithMessage($"Vượt quá độ dài tối đa {B2bsubscriptionPackage.NameMaxLength} ký tự.");
        RuleFor(x => x.Description).MaximumLength(B2bsubscriptionPackage.DescriptionMaxLength)
            .WithMessage($"Vượt quá độ dài tối đa {B2bsubscriptionPackage.DescriptionMaxLength} ký tự.");
        RuleFor(x => x.DurationDays).GreaterThan(0).WithMessage("Thời hạn phải là số ngày nguyên dương.")
            .LessThanOrEqualTo(3650).WithMessage("Thời hạn tối đa 3650 ngày.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Giá phải từ 0 ₫ trở lên.")
            .LessThan(10_000_000_000m).WithMessage("Giá quá lớn.")
            .Must(price => decimal.Round(price, 0) == price).WithMessage("Giá phải là số tiền nguyên (₫).");
        RuleFor(x => x.TransactionLimit).GreaterThan(0).When(x => x.TransactionLimit.HasValue)
            .WithMessage("Giới hạn giao dịch phải là số nguyên dương hoặc để trống (không giới hạn).");
    }
}
