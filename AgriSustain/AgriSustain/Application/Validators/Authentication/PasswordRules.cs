using FluentValidation;

namespace AgriSustain.Application.Validators.Authentication;

internal static class PasswordRules
{
    /// <summary>Password policy shared by registration and password change.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> rule, string? policyMessage = null)
    {
        return rule.NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage(policyMessage ?? "Mật khẩu phải có ít nhất 8 ký tự.")
            .Matches(@"[A-Z]").WithMessage(policyMessage ?? "Mật khẩu phải có ít nhất 1 chữ hoa.")
            .Matches(@"[a-z]").WithMessage(policyMessage ?? "Mật khẩu phải có ít nhất 1 chữ thường.")
            .Matches(@"[0-9]").WithMessage(policyMessage ?? "Mật khẩu phải có ít nhất 1 chữ số.");
    }
}
