using System.Collections.Concurrent;
using AgriSustain.Domain.Entities.Ordering;

namespace AgriSustain.Infrastructure.Data.Demo;

/// <summary>
/// Owns ONLY the dependencies whose real modules/contracts are still missing: fixture session
/// revocation, point balances and DB-06 request keys. Business rows live in SQL. All these mock
/// values reset on restart; they are not a durable loyalty ledger or real JWT/session storage.
/// </summary>
public sealed class SqlDemoDependencies : IDisposable
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal ConcurrentDictionary<long, int> SessionVersions { get; } = new();
    internal Dictionary<long, int> Points { get; private set; } = [];
    internal Dictionary<(long Buyer, Guid Key), RetailOrder> RequestOrders { get; private set; } = [];

    /// <summary>
    /// Publishes mock benefit/key changes only after SQL commits, keeping SQL failures from
    /// consuming local points or publishing an order retry result. The UoW holds Gate.
    /// </summary>
    internal void Publish(
        Dictionary<long, int> points,
        Dictionary<(long Buyer, Guid Key), RetailOrder> orders,
        Dictionary<long, int> sessionVersions)
    {
        Points = points;
        RequestOrders = orders;
        foreach (var pair in sessionVersions)
        {
            SessionVersions[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// Releases the process-local coordination primitive; SQL transaction locks independently
    /// protect persisted mutations. Dependency injection owns disposal.
    /// </summary>
    public void Dispose() => Gate.Dispose();
}
