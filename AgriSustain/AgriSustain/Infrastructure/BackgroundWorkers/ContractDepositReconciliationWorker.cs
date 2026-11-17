using AgriSustain.Infrastructure.Payments;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

public sealed class ContractDepositReconciliationWorker(
    IServiceScopeFactory scopes, ILogger<ContractDepositReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SePayContractDepositReconciler>()
                    .ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                logger.LogWarning("SePay deposit lookup returned HTTP {StatusCode}.", (int)ex.StatusCode.Value);
            }
            catch (Exception ex)
            {
                logger.LogWarning("SePay deposit lookup unavailable ({Type}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
