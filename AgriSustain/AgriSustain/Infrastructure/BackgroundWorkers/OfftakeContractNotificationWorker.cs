using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

/// <summary>Only dispatches committed outbox rows. A retry never repeats the contract action.</summary>
public sealed class OfftakeContractNotificationWorker(IServiceScopeFactory scopes,
    ILogger<OfftakeContractNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
                var gateway = scope.ServiceProvider.GetRequiredService<INotificationGateway>();
                DateTime now = DateTime.UtcNow;
                var ids = await db.Notifications.AsNoTracking().Where(x => x.NotificationType == "FarmerOfftakeContract"
                    && x.DeliveryChannel == "Email" && (x.DeliveryStatus == "Pending"
                    || (x.DeliveryStatus == "Failed" && x.SentAt < now.AddMinutes(-2))
                    || (x.DeliveryStatus == "Sending" && x.SentAt < now.AddMinutes(-5))))
                    .OrderBy(x => x.NotificationId).Select(x => x.NotificationId).Take(20).ToListAsync(stoppingToken);
                foreach (long id in ids)
                {
                    int claimed = await db.Notifications.Where(x => x.NotificationId == id
                        && (x.DeliveryStatus == "Pending" || x.DeliveryStatus == "Failed"
                        || (x.DeliveryStatus == "Sending" && x.SentAt < now.AddMinutes(-5))))
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeliveryStatus, "Sending").SetProperty(x => x.SentAt, now), stoppingToken);
                    if (claimed == 0) continue;
                    var notice = await db.Notifications.Include(x => x.User).SingleAsync(x => x.NotificationId == id, stoppingToken);
                    try
                    {
                        await gateway.SendEmailAsync(notice.User.Email, notice.Title, notice.Content,
                            "offtake-email:" + notice.NotificationId, stoppingToken);
                        notice.DeliveryStatus = "Sent";
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        notice.DeliveryStatus = "Failed";
                        notice.RetryCount++;
                        logger.LogWarning("Offtake email {Id} failed ({Type}); scheduled retry retained.", id, ex.GetType().Name);
                    }
                    notice.SentAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Offtake notification queue unavailable ({Type}).", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
