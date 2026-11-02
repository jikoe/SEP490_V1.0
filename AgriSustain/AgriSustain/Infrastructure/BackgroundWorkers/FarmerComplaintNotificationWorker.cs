using AgriSustain.Application.Services.Complaints;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

public sealed class FarmerComplaintNotificationWorker(
    IServiceScopeFactory scopes,
    ILogger<FarmerComplaintNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<FarmerComplaintService>().NotifySupportAsync(null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Farmer complaint notification queue unavailable ({Type}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
