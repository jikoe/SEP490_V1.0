using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.UnitTests.Authentication;

/// <summary>UC-AUTH-08 Step 1_Email Delivery Failure through the Gmail adapter.</summary>
public sealed class GmailEmailServiceTests
{
    [Fact(DisplayName = "UTC-AUTH08-GM01 An unreachable SMTP server returns false and logs without the code")]
    public async Task Send_SmtpUnreachable_ReturnsFalseAndLogs()
    {
        var logger = new CapturingLogger();
        var service = new GmailEmailService(Options.Create(new GmailOptions
        {
            Host = "127.0.0.1", Port = 1, SenderAddress = "sender@example.test",
            AppPassword = "abcd efgh ijkl mnop", TimeoutSeconds = 5
        }), logger);

        bool sent = await service.SendOtpEmailAsync(
            "buyer@example.test", "482913", OtpPurposes.Registration, default);

        Assert.False(sent);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.DoesNotContain("482913", entry.Message);
    }

    private sealed class CapturingLogger : ILogger<GmailEmailService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
