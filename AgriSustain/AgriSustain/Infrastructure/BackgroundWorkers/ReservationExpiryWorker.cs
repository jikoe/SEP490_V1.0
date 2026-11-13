using AgriSustain.Application.Services.Checkout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    /// <summary>
    /// Hosted-service loop: every 30 seconds runs <see cref="AgriSustain.Application.Services.Checkout.ReservationExpiryService.ExpireAsync"/>
    /// in a new scope; an error is logged and the next tick retries.
    /// </summary>
    /// <param name="stoppingToken">Signalled when the host shuts down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ReservationExpiryService>()
                        .ExpireAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Reservation expiry failed; next batch will retry");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping the host also cancels the wait between batches.
        }
    }
}
