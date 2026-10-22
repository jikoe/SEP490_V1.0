using System.Collections.Concurrent;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Infrastructure.Data.Demo;

public sealed class SqlDemoDependencies
{
    public ConcurrentDictionary<string, PendingRegistration> PendingRegistrations { get; } = new(StringComparer.OrdinalIgnoreCase);
}
