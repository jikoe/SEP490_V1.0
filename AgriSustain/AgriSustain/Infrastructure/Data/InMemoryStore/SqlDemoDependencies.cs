using System.Collections.Concurrent;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Infrastructure.Data.Demo;

public sealed class SqlDemoDependencies
{
    public ConcurrentDictionary<string, PendingRegistration> PendingRegistrations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>UC-AUTH-08 codes keyed by "purpose|email".</summary>
    public ConcurrentDictionary<string, OtpChallenge> OtpChallenges { get; } = new(StringComparer.OrdinalIgnoreCase);
}
