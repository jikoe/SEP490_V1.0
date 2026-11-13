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
    /// <summary>
    /// BR-OTP-01: a code is valid for 5 minutes.
    /// </summary>
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    /// <summary>
    /// BR-OTP-02: wait between two codes for the same purpose and address.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    /// <summary>
    /// BR-OTP-01: wrong entries after which the code is locked.
    /// </summary>
    public const int MaxFailedAttempts = 5;
    /// <summary>
    /// BR-OTP-02: codes an address may receive within <see cref="SendWindow"/>.
    /// </summary>
    public const int MaxSendsPerWindow = 5;
    /// <summary>
    /// BR-OTP-02: rolling window of the send limit.
    /// </summary>
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    // MSG-OTP-03: expired, used, invalidated or locked code.
    /// <summary>
    /// MSG-OTP-03: expired, used, invalidated or locked code.
    /// </summary>
    private const string CodeUnusableMessage =
        "Mã xác thực này không còn dùng được. Hãy chọn \"Gửi lại mã\" để nhận mã mới.";

    // MSG26
    /// <summary>
    /// MSG26: the code e-mail could not be delivered (503).
    /// </summary>
    private static readonly Error s_emailDeliveryFailed = new(
        "EMAIL_DELIVERY_FAILED", "Không thể gửi mã xác thực. Vui lòng thử lại sau.",
        "ServiceUnavailable");

    /// <summary>
    /// Sends a new code for the purpose and address. Refused while the previous code was sent less than 60 seconds ago
    /// (RESEND_COOLDOWN) or when the address already received 5 codes in the last hour, all purposes together
    /// (OTP_SEND_LIMIT, MSG-OTP-06).
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14 "Mã xác thực đã được gửi…"; 429 for cooldown or hourly limit; 503 EMAIL_DELIVERY_FAILED (MSG26).</returns>
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

    /// <summary>
    /// Checks a code, in this order: a code exists (OTP_NOT_FOUND); fewer than 5 failed attempts (MAX_ATTEMPTS_EXCEEDED);
    /// not older than 5 minutes (OTP_EXPIRED); equal to the stored code, compared in constant time (a wrong code adds one
    /// attempt and tells how many remain, MSG-OTP-01); finally the exact code is removed, so it can be used only once
    /// even when two requests race (OTP_ALREADY_USED). An expired or locked code stays stored so the user can resend.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="otpCode">Six digits typed by the user.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True on success; otherwise the error (400, or NotFound which callers turn into their own message).</returns>
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

    /// <summary>
    /// Same as <see cref="IssueAsync"/>, but only when a code was already issued for the purpose and address (NOT_FOUND otherwise).
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14, or the cooldown / limit / delivery error.</returns>
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

    /// <summary>
    /// Drops the current code of the purpose and address, so it can no longer be verified.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    public Task InvalidateAsync(string purpose, string email, CancellationToken cancellationToken) =>
        store.RemoveAsync(purpose, email, cancellationToken);

    /// <summary>
    /// Generates a code and e-mails it first: an undelivered code is never stored and the previous code stays usable.
    /// Once delivered, the code replaces any earlier one for the purpose (so the old one stops working), with 0 attempts
    /// and a 5-minute expiry, and the send is recorded for the hourly limit.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14, or EMAIL_DELIVERY_FAILED.</returns>
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

    /// <summary>
    /// Adds one failed attempt. The update only succeeds if nobody changed the code meanwhile, so concurrent wrong
    /// entries cannot be lost.
    /// </summary>
    /// <param name="challenge">Stored code that did not match.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>INVALID_OTP with the remaining attempts (MSG-OTP-01), or MSG-OTP-03 when this was the last one.</returns>
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

    /// <summary>
    /// MSG-OTP-06: the address already received 5 codes in the last hour; the message says how many minutes to wait.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>OTP_SEND_LIMIT (429), or null when another code may be sent.</returns>
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

    /// <summary>
    /// RESEND_COOLDOWN (429) while the last code is less than 60 seconds old, with the seconds left.
    /// </summary>
    /// <param name="challenge">Current code.</param>
    /// <returns>The error, or null.</returns>
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

    /// <summary>
    /// Constant-time comparison of the stored and typed codes, so response time reveals nothing.
    /// </summary>
    /// <param name="expected">Stored code.</param>
    /// <param name="actual">Typed code (trimmed).</param>
    /// <returns>True when equal.</returns>
    private static bool OtpEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual.Trim()));
}
