using System.Net;
using System.Net.Mail;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Email;

/// <summary>
/// MSG-BA-05 "Your AgriSustain bank account was changed", sent through the same Gmail settings as
/// the OTP e-mail. With Gmail__Provider=Log the e-mail is only written to the server log.
/// A delivery failure is logged and never blocks the saved change.
/// </summary>
public sealed class BankAccountEmailSender(
    IOptions<GmailOptions> options, ILogger<BankAccountEmailSender> logger) : IBankAccountEmailSender
{
    private readonly GmailOptions _options = options.Value;

    public async Task<bool> SendBankAccountChangedAsync(
        string email, string? bankShortName, string? last4, DateTime changedAtUtc,
        CancellationToken cancellationToken)
    {
        string change = bankShortName == null
            ? "đã được xóa"
            : $"đã được đổi thành {bankShortName} •••• {last4}";
        string time = changedAtUtc.ToString("dd/MM/yyyy HH:mm") + " (UTC)";

        if (!_options.UsesGmail)
        {
            logger.LogWarning("[DEV EMAIL] MSG-BA-05 to {Email}: bank account {Change} at {Time}",
                email, change, time);
            return true;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SenderAddress!, _options.SenderName),
            Subject = "AgriSustain - Tài khoản ngân hàng của bạn vừa thay đổi",
            IsBodyHtml = true,
            Body = $"""
                <p>Xin chào,</p>
                <p>Tài khoản ngân hàng trên hồ sơ AgriSustain của bạn {WebUtility.HtmlEncode(change)} lúc {time}.</p>
                <p>Nếu không phải bạn thực hiện, hãy đổi mật khẩu và liên hệ bộ phận hỗ trợ ngay.</p>
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
            logger.LogError(exception, "Gmail could not deliver MSG-BA-05 to {Email}.", email);
            return false;
        }
    }
}
