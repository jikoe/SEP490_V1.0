using AgriSustain.Application.DTOs.Auth;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .StrongPassword()
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Mật khẩu xác nhận không khớp.");
    }
}
