using AgriSustain.Application.Services.B2B;
using AgriSustain.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

/// <summary>UC-WB-08 AF9: hourly check that e-mails renewal reminders 7 and 1 days before EndDate.</summary>
public sealed class SubscriptionReminderWorker(IServiceScopeFactory scopes,
    ILogger<SubscriptionReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IBuyerSubscriptionService>();
                var settings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
                string link = settings.FrontendBaseUrl.TrimEnd('/') + "/WholesaleBuyer/membership.html?renew=1";
                int sent = await service.SendRenewalRemindersAsync(link, stoppingToken);
                if (sent > 0) logger.LogInformation("Sent {Count} subscription renewal reminders.", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Subscription reminders unavailable ({Type}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
