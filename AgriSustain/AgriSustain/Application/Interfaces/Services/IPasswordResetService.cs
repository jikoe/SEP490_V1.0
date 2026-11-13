using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Services;

/// <summary>UC-AUTH-07 Reset Password for a Guest who forgot the password.</summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Step 1. Validates the e-mail, normalises it (trim, lower case) and, only when an account exists, asks UC-AUTH-08 to
    /// send a 6-digit code for the purpose PasswordReset. The answer is always the same MSG-OTP-04 text, whether the
    /// address is registered or not, and even when sending failed (see <c>Neutral</c>), so accounts cannot be guessed.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-OTP-04 "Nếu … là email đã đăng ký, mã xác thực đã được gửi…", or a validation error.</returns>
    Task<Result<string>> RequestCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resend in step 1: UC-AUTH-08 applies the 60-second cooldown and the hourly limit. An address with no pending
    /// request and every other outcome get the same neutral answer as the first request.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The neutral MSG-OTP-04 text, or a validation error.</returns>
    Task<Result<string>> ResendCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Step 2. Checks the 6-digit code through UC-AUTH-08 (5 attempts, 5-minute validity, single use). An unknown e-mail
    /// answers like a wrong code. On success creates a reset token: 32 random bytes in hex, valid 10 minutes. Only its
    /// SHA-256 hash is stored, as an OtpChallenge with the purpose PasswordResetGrant; the plain token goes back to the
    /// browser once.
    /// </summary>
    /// <param name="request">E-mail and 6-digit code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>E-mail, reset token and its expiry; or INVALID_OTP / the lockout and expiry errors of UC-AUTH-08.</returns>
    Task<Result<PasswordResetGrantResponse>> VerifyCodeAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Step 3. Steps: 1) validate the new password (8+ characters with upper, lower case and a digit) and that the
    /// confirmation matches (MSG05); 2) the stored grant must exist, be unexpired and match the token (constant-time
    /// comparison of hashes); 3) the account must exist; 4) the new password must differ from the current one;
    /// 5) remove the grant first, so two concurrent requests cannot both use the same token; 6) store the new hash,
    /// write a ResetPassword audit row and save. Existing sessions and refresh tokens are not revoked.
    /// </summary>
    /// <param name="request">E-mail, reset token, new password and its confirmation.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Success message asking to sign in again; RESET_SESSION_INVALID (401); PASSWORD_REUSED; or a validation error.</returns>
    Task<Result<string>> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken);
}
