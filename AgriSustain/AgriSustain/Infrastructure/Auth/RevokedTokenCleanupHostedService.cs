using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.Auth;

public sealed class RevokedTokenCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevokedTokenCleanupHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public RevokedTokenCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<RevokedTokenCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgriSustainContext>();
                var now = DateTimeOffset.UtcNow;

                var deleted = await db.RevokedTokens
                    .Where(t => t.ExpiresAt <= now)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired revoked tokens from database.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired revoked tokens.");
            }
        }
    }
}

