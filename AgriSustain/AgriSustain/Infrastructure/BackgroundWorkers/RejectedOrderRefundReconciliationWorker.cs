using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Refunds;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

public sealed class RejectedOrderRefundReconciliationWorker(IServiceScopeFactory scopes,
    ILogger<RejectedOrderRefundReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var listScope = scopes.CreateScope();
                var repository = listScope.ServiceProvider.GetRequiredService<IFinanceRejectedOrderRefundRepository>();
                foreach (long id in await repository.PendingAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<FinanceRejectedOrderRefundService>()
                            .ReconcileObservedAsync(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogWarning("Refund {Id} reconciliation unavailable ({Type}).", id, ex.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Refund queue unavailable ({Type}).", ex.GetType().Name); }
        }
    }
}
