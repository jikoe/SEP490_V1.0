using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Options;
namespace AgriSustain.Infrastructure.Email;

public sealed class NotificationGateway(IOptions<EmailSettings> options) : INotificationGateway
{
    public async Task SendEmailAsync(string recipient, string subject, string content,
        string eventKey, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpPass)
            || string.IsNullOrWhiteSpace(settings.SmtpUser)
            || string.IsNullOrWhiteSpace(settings.SmtpHost) || !settings.EnableSsl)
            throw new InvalidOperationException("SMTP credentials and TLS must be configured.");
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.EnableSsl, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPass),
            DeliveryMethod = SmtpDeliveryMethod.Network, Timeout = 30000
        };
        using var message = new MailMessage(new MailAddress(settings.FromEmail, "AgriSustain"),
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
