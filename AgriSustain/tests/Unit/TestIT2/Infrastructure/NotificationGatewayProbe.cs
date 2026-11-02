using Xunit;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.TestIT2.Infrastructure;

namespace AgriSustain.TestIT2.Infrastructure;

internal sealed class MailProbe : INotificationGateway
{
    public int Attempts;

    public bool Fail;

    public Func<bool>? MaySend { get; set; }


    public Task SendEmailAsync(
        string recipient,
        string subject,
        string content,
        string eventKey,
        CancellationToken ct)
    {
        Assert.EndsWith("@example.test", recipient);
        Assert.True(MaySend?.Invoke() ?? true, "Email must be sent after commit.");
        Attempts++;
        if (Fail)
            throw new IOException("Test SMTP failure");
        return Task.CompletedTask;
    }
}
