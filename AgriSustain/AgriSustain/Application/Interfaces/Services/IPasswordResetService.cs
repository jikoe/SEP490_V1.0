using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Services;

/// <summary>UC-AUTH-07 Reset Password for a Guest who forgot the password.</summary>
public interface IPasswordResetService
{
    Task<Result<string>> RequestCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<Result<string>> ResendCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<Result<PasswordResetGrantResponse>> VerifyCodeAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken);

    Task<Result<string>> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken);
}
