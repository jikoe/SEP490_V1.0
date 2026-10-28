namespace AgriSustain.Application.Interfaces.Services;

/// <summary>Sends transactional e-mails (UC-AUTH-08 OTP). Returns false when delivery fails.</summary>
public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string email, string otpCode, CancellationToken cancellationToken);
}

/// <summary>Generates single-use numeric OTP codes.</summary>
public interface IOtpGenerator
{
    string Generate();
}
