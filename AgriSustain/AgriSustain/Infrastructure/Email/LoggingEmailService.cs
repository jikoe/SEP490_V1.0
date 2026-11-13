using AgriSustain.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.Email;

/// <summary>
/// Development e-mail adapter: writes the OTP to the server log instead of sending a real e-mail.
/// The code is never returned to the client. Replace with a Gmail adapter in production.
/// </summary>
public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    /// <summary>
    /// Development only (Gmail:Provider = Log): writes the code to the server log and reports success.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="otpCode">Code to log.</param>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Always true.</returns>
    public Task<bool> SendOtpEmailAsync(
        string email, string otpCode, string purpose, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "[DEV EMAIL] {Purpose} OTP for {Email}: {OtpCode} (valid 5 minutes)",
            purpose,
            email,
            otpCode);
        return Task.FromResult(true);
    }
}
