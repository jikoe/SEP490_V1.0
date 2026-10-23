using System.Collections.Concurrent;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Infrastructure.Data.Demo;

public sealed class SqlDemoDependencies
{
    public ConcurrentDictionary<string, PendingRegistration> PendingRegistrations { get; } = new(StringComparer.OrdinalIgnoreCase);
}
