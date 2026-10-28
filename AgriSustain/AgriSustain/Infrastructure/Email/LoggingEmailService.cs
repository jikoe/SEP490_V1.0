using AgriSustain.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.Email;

/// <summary>
/// Development e-mail adapter: writes the OTP to the server log instead of sending a real e-mail.
/// The code is never returned to the client. Replace with a Gmail adapter in production.
/// </summary>
public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task<bool> SendOtpEmailAsync(string email, string otpCode, CancellationToken cancellationToken)
    {
        logger.LogWarning("[DEV EMAIL] OTP for {Email}: {OtpCode} (valid 5 minutes)", email, otpCode);
        return Task.FromResult(true);
    }
}
