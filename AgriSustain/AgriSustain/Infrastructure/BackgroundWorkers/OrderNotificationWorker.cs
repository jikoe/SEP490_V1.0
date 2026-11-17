using AgriSustain.Application.Services;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

// JOB-09 delivers queued order, refund, account-status and Offtake proposal emails.
public sealed class OrderNotificationWorker(
    IServiceScopeFactory scopes,
    ILogger<OrderNotificationWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Email queue unavailable ({Type}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OrderEmailDispatcher>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var notices = scope.ServiceProvider.GetRequiredService<OrderNotificationService>();
        var now = clock.GetUtcNow().UtcDateTime;
        var retryKeys = await db
            .Notifications.AsNoTracking()
            .Where(n =>
                n.NotificationType == "FarmerOrderUpdate"
                && n.DeliveryStatus == "Failed"
                && n.SentAt <= now.AddMinutes(-1)
            )
            .Select(n => n.PayloadJson)
            .ToListAsync(cancellationToken);
        foreach (var payload in retryKeys.Where(p => p != null).Distinct())
        {
            var marker = payload!.LastIndexOf(':');
            var eventMarker = marker > 0 ? payload.LastIndexOf(':', marker - 1) : -1;
            if (eventMarker > 0)
                await notices.RetryFailedChannelsAsync(payload[..eventMarker], cancellationToken);
        }
        var ids = await db
            .Notifications.AsNoTracking()
            .Where(n =>
                (
                    n.NotificationType == "FarmerOrderUpdate"
                    || n.NotificationType == "RefundEmail"
                    || n.NotificationType == "ComplaintEmail"
                    || n.NotificationType == OfftakeService.ProposalNotificationType
                    || n.NotificationType == OrderEmailDispatcher.AccountEmailType
                        && n.RetryCount < OrderEmailDispatcher.AccountEmailMaxAttempts
                )
                && n.DeliveryChannel == "Email"
                && (
                    (
                        n.DeliveryStatus == "Pending"
                        || n.DeliveryStatus == "Failed" && n.SentAt <= now.AddMinutes(-1)
                    )
                    || n.DeliveryStatus == "Sending" && n.SentAt < now.AddMinutes(-5)
                )
            )
            .OrderBy(n => n.NotificationId)
            .Select(n => n.NotificationId)
            .Take(25)
            .ToListAsync(cancellationToken);
        foreach (long id in ids)
        {
            await dispatcher.DeliverAsync(id, cancellationToken);
        }
    }
}
