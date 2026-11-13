using AgriSustain.Application.Services.Finance;
using AgriSustain.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.BackgroundWorkers;

/// <summary>UC-FIN-06 AF3: marks unpaid contract payment lines past their due date as Overdue and e-mails the buyer.</summary>
public sealed class ContractPaymentOverdueWorker(IServiceScopeFactory scopes,
    ILogger<ContractPaymentOverdueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Checked hourly so a line turns Overdue soon after midnight; each line is marked only once.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IContractPaymentService>();
                var settings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
                int marked = await service.MarkOverdueDailyAsync(settings.FrontendBaseUrl, stoppingToken);
                if (marked > 0) logger.LogInformation("Marked {Count} contract payment lines Overdue.", marked);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Contract payment overdue check unavailable ({Type}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
