using System.Net;
using System.Net.Mail;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Email;

/// <summary>
/// Sends UC-AUTH-08 verification codes through Gmail SMTP (STARTTLS on port 587). A delivery
/// failure is logged without the code and reported as false, so the caller shows MSG26.
/// </summary>
public sealed class GmailEmailService(IOptions<GmailOptions> options, ILogger<GmailEmailService> logger)
    : IEmailService
{
    private readonly GmailOptions _options = options.Value;

    public async Task<bool> SendOtpEmailAsync(
        string email, string otpCode, string purpose, CancellationToken cancellationToken)
    {
        string action = purpose == OtpPurposes.PasswordReset ? "đặt lại mật khẩu" : "xác thực email đăng ký";
        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderAddress!, _options.SenderName),
            Subject = $"AgriSustain - Mã {action}: {otpCode}",
            IsBodyHtml = true,
            Body = $"""
                <p>Xin chào,</p>
                <p>Mã {action} AgriSustain của bạn là:</p>
                <p style="font-size:24px;font-weight:bold;letter-spacing:4px">{otpCode}</p>
                <p>Mã có hiệu lực trong 5 phút và chỉ dùng được một lần. Không chia sẻ mã này với bất kỳ ai.</p>
                <p>Nếu bạn không yêu cầu mã này, hãy bỏ qua email.</p>
                """
        };
        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.SenderAddress,
                _options.AppPassword!.Replace(" ", string.Empty)),
            Timeout = _options.TimeoutSeconds * 1000
        };
        try
        {
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException
            or FormatException)
        {
            // UC-AUTH-08 Step 1_Email Delivery Failure: log the error, never the code.
            logger.LogError(exception, "Gmail could not deliver the {Purpose} OTP e-mail to {Email}.",
                purpose, email);
            return false;
        }
    }
}
