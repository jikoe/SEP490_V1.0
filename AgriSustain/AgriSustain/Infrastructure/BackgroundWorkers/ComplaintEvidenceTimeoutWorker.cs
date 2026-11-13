using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

/// <summary>UC-CS-01: periodically resolves complaints whose 48-hour information request expired.</summary>
public sealed class ComplaintEvidenceTimeoutWorker(IServiceScopeFactory scopes,
    ILogger<ComplaintEvidenceTimeoutWorker> logger) : BackgroundService
{
    /// <summary>
    /// How often the job looks for expired information requests.
    /// </summary>
    private static readonly TimeSpan s_interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Hosted-service loop (JOB-11): every 5 minutes opens a new DI scope, runs
    /// <see cref="IComplaintEvidenceTimeoutProcessor.ResolveOverdueAsync"/> and logs how many complaints were decided.
    /// An error is logged and the next tick retries; the loop only ends when the application stops.
    /// </summary>
    /// <param name="stoppingToken">Signalled when the host shuts down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    int resolved = await scope.ServiceProvider
                        .GetRequiredService<IComplaintEvidenceTimeoutProcessor>()
                        .ResolveOverdueAsync(stoppingToken);
                    if (resolved > 0)
                    {
                        logger.LogInformation(
                            "Resolved {Count} complaints with overdue information requests", resolved);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Complaint evidence timeout failed; next batch will retry");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping the host also cancels the wait between batches.
        }
    }
}
