using System.Collections.Concurrent;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Demo;

namespace AgriSustain.Infrastructure.Data.InMemoryStore;

/// <summary>
/// Process-local UC-AUTH-08 code store, like the pending registrations it accompanies: codes are
/// lost on restart and are not shared between server instances.
/// </summary>
public sealed class InMemoryOtpChallengeStore(SqlDemoDependencies demoStore) : IOtpChallengeStore
{
    public Task<OtpChallenge?> FindAsync(string purpose, string email, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges.TryGetValue(Key(purpose, email), out var challenge);
        return Task.FromResult(challenge);
    }

    public Task SaveAsync(OtpChallenge challenge, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges[Key(challenge.Purpose, challenge.Email)] = challenge;
        return Task.CompletedTask;
    }

    public Task<bool> TryReplaceAsync(
        OtpChallenge current, OtpChallenge next, CancellationToken cancellationToken) =>
        Task.FromResult(demoStore.OtpChallenges.TryUpdate(
            Key(current.Purpose, current.Email), next, current));

    public Task<bool> TryRemoveAsync(OtpChallenge challenge, CancellationToken cancellationToken) =>
        Task.FromResult(demoStore.OtpChallenges.TryRemove(
            new KeyValuePair<string, OtpChallenge>(Key(challenge.Purpose, challenge.Email), challenge)));

    public Task RemoveAsync(string purpose, string email, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges.TryRemove(Key(purpose, email), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DateTimeOffset>> RecordSendAsync(string email, DateTimeOffset sentAt,
        TimeSpan window, CancellationToken cancellationToken)
    {
        var sends = _sends.GetOrAdd(email, _ => []);
        lock (sends)
        {
            sends.RemoveAll(at => at <= sentAt - window);
            sends.Add(sentAt);
            return Task.FromResult<IReadOnlyList<DateTimeOffset>>(sends.ToList());
        }
    }

    public Task<IReadOnlyList<DateTimeOffset>> RecentSendsAsync(string email, DateTimeOffset now,
        TimeSpan window, CancellationToken cancellationToken)
    {
        if (!_sends.TryGetValue(email, out var sends))
        {
            return Task.FromResult<IReadOnlyList<DateTimeOffset>>([]);
        }
        lock (sends)
        {
            return Task.FromResult<IReadOnlyList<DateTimeOffset>>(
                sends.Where(at => at > now - window).OrderBy(at => at).ToList());
        }
    }

    private static string Key(string purpose, string email) => $"{purpose}|{email}";

    // Delivered-code times per address for the BR-OTP-02 hourly limit; process-local like the codes.
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _sends =
        new(StringComparer.OrdinalIgnoreCase);
}
