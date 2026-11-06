using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;

namespace AgriSustain.Application.Services.Authentication;

/// <summary>
/// UC-AUTH-08 Verify Email OTP: issues a 6-digit single-use code valid for 5 minutes, verifies it
/// (correct, unexpired, unused, fewer than 5 failed attempts) and resends it after a 60-second cooldown,
/// at most 5 codes per address within one hour (BR-OTP-01, BR-OTP-02).
/// An expired or locked code stays in place so the user can request a new one without restarting
/// the calling use case.
/// </summary>
public sealed class EmailOtpVerifier(
    IOtpChallengeStore store,
    IEmailService emailService,
    IOtpGenerator otpGenerator,
    TimeProvider timeProvider) : IEmailOtpVerifier
{
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxFailedAttempts = 5;
    public const int MaxSendsPerWindow = 5;
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    // MSG-OTP-03: expired, used, invalidated or locked code.
    private const string CodeUnusableMessage =
        "Mã xác thực này không còn dùng được. Hãy chọn \"Gửi lại mã\" để nhận mã mới.";

    // MSG26
    private static readonly Error s_emailDeliveryFailed = new(
        "EMAIL_DELIVERY_FAILED", "Không thể gửi mã xác thực. Vui lòng thử lại sau.",
        "ServiceUnavailable");

    public async Task<Result<string>> IssueAsync(
        string purpose, string email, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(purpose, email, cancellationToken);
        if (existing is not null && CooldownError(existing) is { } cooldown)
        {
            return Result<string>.FailureResult(cooldown);
        }
        if (await HourlyLimitErrorAsync(email, cancellationToken) is { } limit)
        {
            return Result<string>.FailureResult(limit);
        }

        return await SendNewCodeAsync(purpose, email, cancellationToken);
    }

    public async Task<Result<bool>> VerifyAsync(
        string purpose, string email, string otpCode, CancellationToken cancellationToken)
    {
        var challenge = await store.FindAsync(purpose, email, cancellationToken);
        if (challenge is null)
        {
            return Result<bool>.FailureResult(new Error(
                "OTP_NOT_FOUND", CodeUnusableMessage, "NotFound"));
        }

        if (challenge.AttemptCount >= MaxFailedAttempts)
        {
            return Result<bool>.FailureResult(new Error(
                "MAX_ATTEMPTS_EXCEEDED", CodeUnusableMessage));
        }

        if (timeProvider.GetUtcNow() > challenge.ExpiresAt)
        {
            return Result<bool>.FailureResult(new Error(
                "OTP_EXPIRED", CodeUnusableMessage));
        }

        if (!OtpEquals(challenge.OtpCode, otpCode))
        {
            return await RecordFailedAttemptAsync(challenge, cancellationToken);
        }

        // Removing the exact code makes it single-use even when two requests race.
        if (!await store.TryRemoveAsync(challenge, cancellationToken))
        {
            return Result<bool>.FailureResult(new Error(
                "OTP_ALREADY_USED", CodeUnusableMessage));
        }

        return Result<bool>.SuccessResult(true);
    }

    public async Task<Result<string>> ResendAsync(
        string purpose, string email, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(purpose, email, cancellationToken);
        if (existing is null)
        {
            return Result<string>.FailureResult(new Error(
                "NOT_FOUND", "Không tìm thấy yêu cầu xác thực cho email này. Vui lòng thực hiện lại.",
                "NotFound"));
        }

        if (CooldownError(existing) is { } cooldown)
        {
            return Result<string>.FailureResult(cooldown);
        }
        if (await HourlyLimitErrorAsync(email, cancellationToken) is { } limit)
        {
            return Result<string>.FailureResult(limit);
        }

        return await SendNewCodeAsync(purpose, email, cancellationToken);
    }

    public Task InvalidateAsync(string purpose, string email, CancellationToken cancellationToken) =>
        store.RemoveAsync(purpose, email, cancellationToken);

    private async Task<Result<string>> SendNewCodeAsync(
        string purpose, string email, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        string otp = otpGenerator.Generate();

        // Send first: an undelivered code is never stored, and the previous code stays as it was.
        if (!await emailService.SendOtpEmailAsync(email, otp, purpose, cancellationToken))
        {
            return Result<string>.FailureResult(s_emailDeliveryFailed);
        }

        // Saving replaces (and so invalidates) any earlier code for the same purpose.
        await store.SaveAsync(
            new OtpChallenge(purpose, email, otp, now.Add(OtpLifetime), 0, now), cancellationToken);
        await store.RecordSendAsync(email, now, SendWindow, cancellationToken);
        // MSG14
        return Result<string>.SuccessResult(
            $"Mã xác thực đã được gửi tới {email}. Mã có hiệu lực trong 5 phút.");
    }

    private async Task<Result<bool>> RecordFailedAttemptAsync(
        OtpChallenge challenge, CancellationToken cancellationToken)
    {
        int attempts = challenge.AttemptCount + 1;
        if (!await store.TryReplaceAsync(
            challenge, challenge with { AttemptCount = attempts }, cancellationToken))
        {
            return Result<bool>.FailureResult(new Error(
                "INVALID_OTP", "Mã OTP không chính xác. Vui lòng thử lại."));
        }

        int remaining = MaxFailedAttempts - attempts;
        return Result<bool>.FailureResult(new Error(
            "INVALID_OTP",
            remaining > 0
                ? $"Mã xác thực không chính xác. Bạn còn {remaining} lần thử." // MSG-OTP-01
                : CodeUnusableMessage));
    }

    /// <summary>MSG-OTP-06: the address already received 5 codes in the last hour.</summary>
    private async Task<Error?> HourlyLimitErrorAsync(string email, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sends = await store.RecentSendsAsync(email, now, SendWindow, cancellationToken);
        if (sends.Count < MaxSendsPerWindow)
        {
            return null;
        }
        var wait = sends[sends.Count - MaxSendsPerWindow].Add(SendWindow) - now;
        return new Error(
            "OTP_SEND_LIMIT",
            $"Email này đã yêu cầu quá nhiều mã. Vui lòng thử lại sau {Math.Max(1, Math.Ceiling(wait.TotalMinutes))} phút.",
            "TooManyRequests");
    }

    private Error? CooldownError(OtpChallenge challenge)
    {
        var wait = challenge.SentAt.Add(ResendCooldown) - timeProvider.GetUtcNow();
        return wait > TimeSpan.Zero
            ? new Error(
                "RESEND_COOLDOWN",
                $"Vui lòng chờ {Math.Ceiling(wait.TotalSeconds)} giây trước khi gửi lại mã.",
                "TooManyRequests")
            : null;
    }

    private static bool OtpEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual.Trim()));
}
