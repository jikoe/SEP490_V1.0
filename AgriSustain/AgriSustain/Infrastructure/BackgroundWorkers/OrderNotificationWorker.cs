using AgriSustain.Application.Services;
using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AgriSustain.Infrastructure.BackgroundWorkers;

// This worker owns only product-submission emails; other teams' notifications are untouched.
public sealed class OrderNotificationWorker(IServiceScopeFactory scopes,
    ILogger<OrderNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { await DispatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Product email queue unavailable ({Type}).", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<INotificationGateway>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var notices = scope.ServiceProvider.GetRequiredService<OrderNotificationService>();
        var now = clock.GetUtcNow().UtcDateTime;
        var retryKeys = await db.Notifications.AsNoTracking().Where(n =>
            n.NotificationType == "FarmerOrderUpdate" && n.DeliveryStatus == "Failed"
            && n.SentAt <= now.AddMinutes(-1)).Select(n => n.PayloadJson).ToListAsync(cancellationToken);
        foreach (var payload in retryKeys.Where(p => p != null).Distinct())
        {
            var marker = payload!.LastIndexOf(':');
            var eventMarker = marker > 0 ? payload.LastIndexOf(':', marker - 1) : -1;
            if (eventMarker > 0) await notices.RetryFailedChannelsAsync(payload[..eventMarker], cancellationToken);
        }
        var ids = await db.Notifications.AsNoTracking().Where(n =>
            n.NotificationType == "FarmerOrderUpdate" && n.DeliveryChannel == "Email"
            && ((n.DeliveryStatus == "Pending" || n.DeliveryStatus == "Failed" && n.SentAt <= now.AddMinutes(-1))
                || n.DeliveryStatus == "Sending" && n.SentAt < now.AddMinutes(-5)))
            .OrderBy(n => n.NotificationId).Select(n => n.NotificationId).Take(25)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            // Atomic claim prevents multiple application instances from sending the same queued row.
            int claimed = await db.Notifications.Where(n => n.NotificationId == id
                && ((n.DeliveryStatus == "Pending" || n.DeliveryStatus == "Failed" && n.SentAt <= now.AddMinutes(-1))
                    || n.DeliveryStatus == "Sending" && n.SentAt < now.AddMinutes(-5)))
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.DeliveryStatus, "Sending")
                    .SetProperty(n => n.SentAt, now), cancellationToken);
            if (claimed == 0) continue;
            var notification = await db.Notifications.Include(n => n.User)
                .SingleAsync(n => n.NotificationId == id, cancellationToken);
            try
            {
                await gateway.SendEmailAsync(notification.User.Email,notification.Title,notification.Content,
                    notification.PayloadJson ?? $"order-notice:{id}",cancellationToken);
                notices.RecordDeliveryResult(notification, true, clock.GetUtcNow().UtcDateTime);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                notices.RecordDeliveryResult(notification, false, clock.GetUtcNow().UtcDateTime);
                logger.LogWarning("Product email {Id} pending retry ({Type}).", id, ex.GetType().Name);
            }
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }
}
