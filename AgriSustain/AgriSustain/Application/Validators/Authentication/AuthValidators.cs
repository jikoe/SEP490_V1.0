using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

public sealed partial class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Họ và tên không được để trống.")
            .Must(name => name is null || name.Trim().Length is >= 2 and <= 50)
            .WithMessage("Họ và tên phải từ 2 đến 50 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Must(phone => phone is not null && Regex.IsMatch(phone.Trim(), @"^0\d{9}$"))
            .WithMessage(SystemMessages.Msg180InvalidPhone);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .Must(email => email is not null && EmailPattern().IsMatch(email.Trim()))
            .WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.");

        RuleFor(x => x.Password).StrongPassword();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Mật khẩu xác nhận không khớp.");

        RuleFor(x => x.Role)
            .Must(r => r is "RetailCustomer" or "Farmer")
            .WithMessage("Vai trò phải là 'RetailCustomer' hoặc 'Farmer'.");

        RuleFor(x => x.AgreeTerms)
            .Equal(true).WithMessage("Bạn cần đồng ý với điều khoản sử dụng.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Mã OTP không được để trống.")
            .Length(6).WithMessage("Mã OTP phải có đúng 6 ký tự.")
            .Matches(@"^\d{6}$").WithMessage("Mã OTP chỉ bao gồm chữ số.");
    }
}

public sealed class ResendOtpRequestValidator : AbstractValidator<ResendOtpRequest>
{
    public ResendOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.");
    }
}
