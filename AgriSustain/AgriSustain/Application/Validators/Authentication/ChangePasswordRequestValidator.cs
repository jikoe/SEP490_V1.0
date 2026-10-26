using AgriSustain.Application.DTOs.Auth;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.").WithErrorCode("MSG02");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.").WithErrorCode("MSG02")
            .StrongPassword("Mật khẩu mới không đáp ứng các tiêu chí bảo mật bắt buộc.")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Mật khẩu mới không được trùng với mật khẩu hiện tại.")
            .WithErrorCode("MSG12");

        RuleFor(x => x.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng xác nhận mật khẩu mới.").WithErrorCode("MSG02")
            .Equal(x => x.NewPassword).WithMessage("Mật khẩu xác nhận không khớp.")
            .WithErrorCode("MSG05");
    }
}
