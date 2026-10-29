using AgriSustain.Application.DTOs.Authentication;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Phiên đặt lại mật khẩu không hợp lệ. Vui lòng xác thực lại email.");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.").WithErrorCode("MSG02")
            .StrongPassword("Mật khẩu mới không đáp ứng các tiêu chí bảo mật bắt buộc.");

        RuleFor(x => x.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng xác nhận mật khẩu mới.").WithErrorCode("MSG02")
            .Equal(x => x.NewPassword).WithMessage("Mật khẩu xác nhận không khớp.")
            .WithErrorCode("MSG05");
    }
}
