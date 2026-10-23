using System.Net.Http.Headers;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgriSustain.IntegrationTests;

// These exercise the real HTTP/Application pipeline with authorized demo adapters.
// They do not test SQL Server persistence or real JWT/payment/delivery integrations.
/// <summary>
/// Creates isolated in-process HTTP/application fixtures with an injectable seed, server clock
/// and failure decorators; it does not connect to the business SQL database.
/// </summary>
internal sealed class ApiFactory(Action<DemoState>? configureSeed = null,
    Func<IThreeUcSession, IThreeUcSession>? decorateSession = null)
    : WebApplicationFactory<Program>
{
    public TestClock Clock { get; } = new();

    /// <summary>
    /// Forces Development, replaces the store/clock with isolated instances, and optionally
    /// decorates session boundaries for rollback/stale-data tests.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Persistence:Mode"] = "InMemory" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IThreeUcUnitOfWork>();
            services.RemoveAll<IDemoIdentityReader>();
            services.AddSingleton<IThreeUcUnitOfWork>(provider => provider.GetRequiredService<DemoUnitOfWork>());
            services.AddSingleton<IDemoIdentityReader>(provider => provider.GetRequiredService<DemoUnitOfWork>());
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.Replace(ServiceDescriptor.Singleton<DemoUnitOfWork>(_ =>
            {
                var seed = DemoState.Seed(Clock.GetUtcNow());
                configureSeed?.Invoke(seed);
                return new DemoUnitOfWork(Clock, seed);
            }));
            if (decorateSession != null)
            {
                services.Replace(ServiceDescriptor.Singleton<IThreeUcUnitOfWork>(provider =>
                    new ScenarioUnitOfWork(
                        provider.GetRequiredService<DemoUnitOfWork>(), decorateSession)));
            }
        });
    }

    /// <summary>
    /// Builds a test HTTP client with an optional public demo selector; null intentionally sends
    /// an unauthenticated request.
    /// </summary>
    public HttpClient Client(string? fixture = "admin")
    {
        var client = CreateClient();
        if (fixture != null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Demo", fixture);
        }

        return client;
    }

    public DemoUnitOfWork Store => Services.GetRequiredService<DemoUnitOfWork>();
}

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-18T03:00:00Z");
    /// <summary>
    /// Returns the deterministic test instant so quote/lock expiry assertions do not depend on
    /// wall-clock delays.
    /// </summary>
    public override DateTimeOffset GetUtcNow() => _now;
    /// <summary>
    /// Moves only the isolated test clock to exercise expiration boundaries without sleeping.
    /// </summary>
    public void Advance(TimeSpan duration) => _now += duration;
}
