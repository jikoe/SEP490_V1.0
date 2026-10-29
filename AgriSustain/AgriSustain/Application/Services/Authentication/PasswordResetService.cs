using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using FluentValidation;

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
    TimeProvider timeProvider) : IPasswordResetService
{
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(10);
    private const int ResetTokenBytes = 32;

    private static readonly Error s_invalidCode = new(
        "INVALID_OTP", "Mã OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại hoặc gửi lại mã.");

    private static readonly Error s_invalidResetSession = new(
        "RESET_SESSION_INVALID",
        "Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng xác thực lại email.",
        "Unauthorized");

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
        return issued.IsSuccess ? Result<string>.SuccessResult(CodeSentMessage(email)) : issued;
    }

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
        return resent.IsSuccess || resent.Error?.Kind == "NotFound"
            ? Result<string>.SuccessResult(CodeSentMessage(email))
            : resent;
    }

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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string CodeSentMessage(string email) =>
        $"Nếu {email} là email đã đăng ký, mã xác thực đã được gửi tới email này. "
            + "Mã có hiệu lực trong 5 phút.";

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TokenMatches(string expectedHash, string token) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash), Encoding.UTF8.GetBytes(HashToken(token.Trim())));
}
