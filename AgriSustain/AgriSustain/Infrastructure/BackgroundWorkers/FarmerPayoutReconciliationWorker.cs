using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

public sealed class FarmerPayoutReconciliationWorker(IServiceScopeFactory scopes,
    IConfiguration configuration, ILogger<FarmerPayoutReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                string? token = configuration["Payouts:SePayApiToken"] ?? configuration["Payment:SePay:ApiToken"];
                if (string.IsNullOrWhiteSpace(token)) continue;
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IFinanceFarmerPayoutRepository>();
                var ids = await repository.PendingAsync(stoppingToken);
                foreach (long id in ids)
                {
                    // Separate scopes prevent stale entity instances after another worker settles a payout.
                    using var itemScope = scopes.CreateScope();
                    await itemScope.ServiceProvider.GetRequiredService<FinanceFarmerPayoutService>()
                        .ReconcileSystemAsync(id, stoppingToken);
                    await Task.Delay(650, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Payout status query unavailable ({Type}). Sources remain held.", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
