using AgriSustain.Application.Services;
using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AgriSustain.Infrastructure.BackgroundWorkers;

// Sends the queued order-update, refund (UC-FIN-03, MSG-RF-08/09) and account-status (UC-ADM-03,
// BR-UM-11) emails; other notifications are untouched.
public sealed class OrderNotificationWorker(IServiceScopeFactory scopes,
    ILogger<OrderNotificationWorker> logger) : BackgroundService
{
    private const string AccountEmailType = AgriSustain.Application.Services.Administration
        .AdministrationService.AccountEmailType;

    // JOB-09 retries an account email until RetryCount reaches 3, then marks it PermanentFailure.
    private const int AccountEmailMaxAttempts = 3;

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
            (n.NotificationType == "FarmerOrderUpdate" || n.NotificationType == "RefundEmail"
                || n.NotificationType == AccountEmailType && n.RetryCount < AccountEmailMaxAttempts)
            && n.DeliveryChannel == "Email"
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
                if (notification.NotificationType == AccountEmailType
                    && notification.RetryCount >= AccountEmailMaxAttempts)
                {
                    // JOB-09: stop after the retry limit and record the final failure.
                    notification.DeliveryStatus = "PermanentFailure";
                    logger.LogError("Account email {Id} failed after {Attempts} attempts.", id,
                        notification.RetryCount);
                }
                logger.LogWarning("Product email {Id} pending retry ({Type}).", id, ex.GetType().Name);
            }
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }
}
