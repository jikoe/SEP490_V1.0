using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriSustain.Application.Services.Authentication;

/// <summary>
/// UC-AUTH-07 Reset Password. Step 1 sends a code through UC-AUTH-08, step 2 exchanges the verified
/// code for a short-lived single-use reset token, step 3 stores the new password hash. Responses for
/// unknown e-mail addresses look the same as for registered ones so accounts cannot be enumerated.
/// </summary>
public sealed class PasswordResetService(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IEmailOtpVerifier otpVerifier,
    IOtpChallengeStore grantStore,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<VerifyOtpRequest> verifyOtpValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator,
    TimeProvider timeProvider,
    ILogger<PasswordResetService>? logger = null) : IPasswordResetService
{
    private readonly ILogger _logger = logger ?? NullLogger<PasswordResetService>.Instance;

    /// <summary>
    /// How long the reset token from step 2 stays valid.
    /// </summary>
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(10);
    /// <summary>
    /// Random bytes in a reset token (64 hex characters).
    /// </summary>
    private const int ResetTokenBytes = 32;

    private static readonly Error s_invalidCode = new(
        "INVALID_OTP", "Mã OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại hoặc gửi lại mã.");

    private static readonly Error s_invalidResetSession = new(
        "RESET_SESSION_INVALID",
        "Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng xác thực lại email.",
        "Unauthorized");

    /// <summary>
    /// Step 1. Validates the e-mail, normalises it (trim, lower case) and, only when an account exists, asks UC-AUTH-08 to
    /// send a 6-digit code for the purpose PasswordReset. The answer is always the same MSG-OTP-04 text, whether the
    /// address is registered or not, and even when sending failed (see <c>Neutral</c>), so accounts cannot be guessed.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-OTP-04 "Nếu … là email đã đăng ký, mã xác thực đã được gửi…", or a validation error.</returns>
    public async Task<Result<string>> RequestCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            forgotPasswordValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        if (await userRepository.FindByEmailAsync(email, cancellationToken) is null)
        {
            return Result<string>.SuccessResult(CodeSentMessage(email));
        }

        var issued = await otpVerifier.IssueAsync(OtpPurposes.PasswordReset, email, cancellationToken);
        return Neutral(issued, email, "request");
    }

    /// <summary>
    /// Resend in step 1: UC-AUTH-08 applies the 60-second cooldown and the hourly limit. An address with no pending
    /// request and every other outcome get the same neutral answer as the first request.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The neutral MSG-OTP-04 text, or a validation error.</returns>
    public async Task<Result<string>> ResendCodeAsync(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            forgotPasswordValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        var resent = await otpVerifier.ResendAsync(OtpPurposes.PasswordReset, email, cancellationToken);
        return resent.Error?.Kind == "NotFound"
            ? Result<string>.SuccessResult(CodeSentMessage(email))
            : Neutral(resent, email, "resend");
    }

    /// <summary>
    /// GBR-04 / BR-OTP-03: cooldown, hourly limit and delivery errors only happen for registered
    /// addresses, so answering them would reveal the account. Every outcome gets the same MSG-OTP-04
    /// as an unknown address; failures are only logged.
    /// </summary>
    /// <param name="outcome">Result from UC-AUTH-08.</param>
    /// <param name="email">Normalised e-mail.</param>
    /// <param name="step">"request" or "resend", for the log.</param>
    /// <returns>Always the neutral success message.</returns>
    private Result<string> Neutral(Result<string> outcome, string email, string step)
    {
        if (!outcome.IsSuccess)
        {
            _logger.LogWarning("Password-reset OTP {Step} not sent ({Code}) for a registered account.",
                step, outcome.Error?.Code);
        }
        return Result<string>.SuccessResult(CodeSentMessage(email));
    }

    /// <summary>
    /// Step 2. Checks the 6-digit code through UC-AUTH-08 (5 attempts, 5-minute validity, single use). An unknown e-mail
    /// answers like a wrong code. On success creates a reset token: 32 random bytes in hex, valid 10 minutes. Only its
    /// SHA-256 hash is stored, as an OtpChallenge with the purpose PasswordResetGrant; the plain token goes back to the
    /// browser once.
    /// </summary>
    /// <param name="request">E-mail and 6-digit code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>E-mail, reset token and its expiry; or INVALID_OTP / the lockout and expiry errors of UC-AUTH-08.</returns>
    public async Task<Result<PasswordResetGrantResponse>> VerifyCodeAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(verifyOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<PasswordResetGrantResponse>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        var verified = await otpVerifier.VerifyAsync(
            OtpPurposes.PasswordReset, email, request.OtpCode, cancellationToken);
        if (!verified.IsSuccess)
        {
            // An unknown e-mail never had a code; answer like a wrong code.
            return Result<PasswordResetGrantResponse>.FailureResult(
                verified.Error?.Kind == "NotFound" ? s_invalidCode : verified.Error!);
        }

        var now = timeProvider.GetUtcNow();
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(ResetTokenBytes));
        var expiresAt = now.Add(ResetTokenLifetime);
        await grantStore.SaveAsync(new OtpChallenge(
            OtpPurposes.PasswordResetGrant, email, HashToken(token), expiresAt, 0, now), cancellationToken);

        return Result<PasswordResetGrantResponse>.SuccessResult(
            new PasswordResetGrantResponse(email, token, expiresAt));
    }

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
    public async Task<Result<string>> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            resetPasswordValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        var grant = await grantStore.FindAsync(OtpPurposes.PasswordResetGrant, email, cancellationToken);
        if (grant is null
            || timeProvider.GetUtcNow() > grant.ExpiresAt
            || !TokenMatches(grant.OtpCode, request.ResetToken))
        {
            return Result<string>.FailureResult(s_invalidResetSession);
        }

        var user = await userRepository.FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return Result<string>.FailureResult(s_invalidResetSession);
        }

        if (passwordService.Verify(user, request.NewPassword).IsValid)
        {
            return Result<string>.FailureResult(new Error(
                "PASSWORD_REUSED",
                "Mật khẩu mới không được trùng với mật khẩu hiện tại.",
                Fields: new Dictionary<string, string[]>
                {
                    ["newPassword"] = ["Mật khẩu mới không được trùng với mật khẩu hiện tại."]
                }));
        }

        // Consuming the grant first makes the token single-use even when two requests race.
        if (!await grantStore.TryRemoveAsync(grant, cancellationToken))
        {
            return Result<string>.FailureResult(s_invalidResetSession);
        }

        user.PasswordHash = passwordService.Hash(user, request.NewPassword);
        userRepository.AddAudit(new AuditLog
        {
            PerformedByUserId = user.UserId,
            EntityType = "User",
            EntityId = user.UserId,
            Action = "ResetPassword",
            OccurredAt = timeProvider.GetUtcNow().UtcDateTime
        });
        await userRepository.SaveChangesAsync(cancellationToken);
        return Result<string>.SuccessResult(
            "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    /// <summary>
    /// Trims and lower-cases the e-mail so codes and grants are found whatever the typing.
    /// </summary>
    /// <param name="email">E-mail as typed.</param>
    /// <returns>Normalised e-mail.</returns>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// MSG-OTP-04, the neutral "code sent if registered" message with the 5-minute validity.
    /// </summary>
    /// <param name="email">Normalised e-mail.</param>
    /// <returns>Message text.</returns>
    private static string CodeSentMessage(string email) =>
        $"Nếu {email} là email đã đăng ký, mã xác thực đã được gửi tới email này. "
            + "Mã có hiệu lực trong 5 phút.";

    /// <summary>
    /// SHA-256 of the reset token in hex; only this hash is stored.
    /// </summary>
    /// <param name="token">Plain token.</param>
    /// <returns>Hash.</returns>
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Compares the stored hash with the hash of the sent token in constant time, so timing reveals nothing.
    /// </summary>
    /// <param name="expectedHash">Stored hash.</param>
    /// <param name="token">Token from the request.</param>
    /// <returns>True when they match.</returns>
    private static bool TokenMatches(string expectedHash, string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash), Encoding.UTF8.GetBytes(HashToken(token.Trim())));
}
