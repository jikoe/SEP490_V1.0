using AgriSustain.Application.Common;

namespace AgriSustain.Application.Interfaces.Services;

/// <summary>Sends transactional e-mails (UC-AUTH-08 OTP). Returns false when delivery fails.</summary>
public interface IEmailService
{
    /// <summary>
    /// Sends the code e-mail for a purpose.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="otpCode">Code to send.</param>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when delivered, false when delivery failed.</returns>
    Task<bool> SendOtpEmailAsync(
        string email, string otpCode, string purpose, CancellationToken cancellationToken);
}

/// <summary>Generates single-use numeric OTP codes.</summary>
public interface IOtpGenerator
{
    /// <summary>
    /// Creates a new random 6-digit code.
    /// </summary>
    /// <returns>Six digits, leading zeros kept.</returns>
    string Generate();
}

/// <summary>
/// UC-AUTH-08 Verify Email OTP, included by UC-AUTH-01, UC-AUTH-02 and UC-AUTH-07.
/// The calling use case validates its own input and normalizes the e-mail first.
/// </summary>
public interface IEmailOtpVerifier
{
    /// <summary>
    /// Sends a new code for the purpose and address. Refused while the previous code was sent less than 60 seconds ago
    /// (RESEND_COOLDOWN) or when the address already received 5 codes in the last hour, all purposes together
    /// (OTP_SEND_LIMIT, MSG-OTP-06).
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14 "Mã xác thực đã được gửi…"; 429 for cooldown or hourly limit; 503 EMAIL_DELIVERY_FAILED (MSG26).</returns>
    Task<Result<string>> IssueAsync(string purpose, string email, CancellationToken cancellationToken);

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
    Task<Result<bool>> VerifyAsync(
        string purpose, string email, string otpCode, CancellationToken cancellationToken);

    /// <summary>
    /// Same as <see cref="IssueAsync"/>, but only when a code was already issued for the purpose and address (NOT_FOUND otherwise).
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14, or the cooldown / limit / delivery error.</returns>
    Task<Result<string>> ResendAsync(string purpose, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the current code of the purpose and address, so it can no longer be verified.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    Task InvalidateAsync(string purpose, string email, CancellationToken cancellationToken);
}
