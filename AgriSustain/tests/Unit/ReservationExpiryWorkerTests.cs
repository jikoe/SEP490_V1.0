using AgriSustain.Infrastructure.BackgroundWorkers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class ReservationExpiryWorkerTests
{
    [Fact]
    public async Task StopBeforeFirstTick_CompletesWorkerSuccessfully()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var worker = new ReservationExpiryWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReservationExpiryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);

        await worker.StopAsync(CancellationToken.None);

        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task CancelStartupTokenBeforeFirstTick_CompletesWorkerSuccessfully()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        using var worker = new ReservationExpiryWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ReservationExpiryWorker>.Instance);
        await worker.StartAsync(cancellation.Token);

        cancellation.Cancel();

        try
        {
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
