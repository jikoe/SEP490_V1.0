using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Options;
namespace AgriSustain.Infrastructure.Email;

public sealed class NotificationGateway(IOptions<GmailOptions> options) : INotificationGateway
{
    public async Task SendEmailAsync(string recipient, string subject, string content,
        string eventKey, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.UsesGmail || string.IsNullOrWhiteSpace(settings.AppPassword)
            || string.IsNullOrWhiteSpace(settings.SenderAddress)
            || string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("Configure Gmail__SenderAddress and Gmail__AppPassword with Gmail provider enabled.");
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.SenderAddress,
                settings.AppPassword.Replace(" ", string.Empty)),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Clamp(settings.TimeoutSeconds, 1, 120) * 1000
        };
        using var message = new MailMessage(new MailAddress(settings.SenderAddress, settings.SenderName),
            new MailAddress(recipient))
        {
            Subject = subject, Body = content, IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8
        };
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(eventKey)));
        message.Headers.Add("Message-ID", $"<{key}@agrisustain.notification>");
        await client.SendMailAsync(message, cancellationToken);
    }
}
