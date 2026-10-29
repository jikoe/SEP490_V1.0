using AgriSustain.Application.Common;

namespace AgriSustain.Application.Interfaces.Services;

/// <summary>Sends transactional e-mails (UC-AUTH-08 OTP). Returns false when delivery fails.</summary>
public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(
        string email, string otpCode, string purpose, CancellationToken cancellationToken);
}

/// <summary>Generates single-use numeric OTP codes.</summary>
public interface IOtpGenerator
{
    string Generate();
}

/// <summary>
/// UC-AUTH-08 Verify Email OTP, included by UC-AUTH-01, UC-AUTH-02 and UC-AUTH-07.
/// The calling use case validates its own input and normalizes the e-mail first.
/// </summary>
public interface IEmailOtpVerifier
{
    Task<Result<string>> IssueAsync(string purpose, string email, CancellationToken cancellationToken);

    Task<Result<bool>> VerifyAsync(
        string purpose, string email, string otpCode, CancellationToken cancellationToken);

    Task<Result<string>> ResendAsync(string purpose, string email, CancellationToken cancellationToken);

    Task InvalidateAsync(string purpose, string email, CancellationToken cancellationToken);
}
