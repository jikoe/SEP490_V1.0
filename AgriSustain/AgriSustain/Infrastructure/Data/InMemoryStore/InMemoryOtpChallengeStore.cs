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
    /// <summary>
    /// Reads the code from the shared in-memory dictionary (key "purpose|email").
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The code, or null.</returns>
    public Task<OtpChallenge?> FindAsync(string purpose, string email, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges.TryGetValue(Key(purpose, email), out var challenge);
        return Task.FromResult(challenge);
    }

    /// <summary>
    /// Overwrites the entry of the purpose and address.
    /// </summary>
    /// <param name="challenge">Code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    public Task SaveAsync(OtpChallenge challenge, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges[Key(challenge.Purpose, challenge.Email)] = challenge;
        return Task.CompletedTask;
    }

    /// <summary>
    /// ConcurrentDictionary.TryUpdate: swaps only if the stored value is still <paramref name="current"/>.
    /// </summary>
    /// <param name="current">Expected value.</param>
    /// <param name="next">New value.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when replaced.</returns>
    public Task<bool> TryReplaceAsync(
        OtpChallenge current, OtpChallenge next, CancellationToken cancellationToken) =>
        Task.FromResult(demoStore.OtpChallenges.TryUpdate(
            Key(current.Purpose, current.Email), next, current));

    /// <summary>
    /// Removes the entry only if it still holds this exact code, which makes a code single-use.
    /// </summary>
    /// <param name="challenge">Code as read.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when removed.</returns>
    public Task<bool> TryRemoveAsync(OtpChallenge challenge, CancellationToken cancellationToken) =>
        Task.FromResult(demoStore.OtpChallenges.TryRemove(
            new KeyValuePair<string, OtpChallenge>(Key(challenge.Purpose, challenge.Email), challenge)));

    /// <summary>
    /// Removes the entry whatever it holds.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    public Task RemoveAsync(string purpose, string email, CancellationToken cancellationToken)
    {
        demoStore.OtpChallenges.TryRemove(Key(purpose, email), out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Under a lock per address: drops send times older than the window, adds this one, returns a copy.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="sentAt">Delivery time.</param>
    /// <param name="window">Window.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Send times inside the window.</returns>
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

    /// <summary>
    /// Send times newer than now − window, oldest first.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="now">Current time.</param>
    /// <param name="window">Window.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Send times.</returns>
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

    /// <summary>
    /// Dictionary key of a code: "purpose|email".
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <returns>Key.</returns>
    private static string Key(string purpose, string email) => $"{purpose}|{email}";

    // Delivered-code times per address for the BR-OTP-02 hourly limit; process-local like the codes.
    /// <summary>
    /// Delivered-code times per address for the BR-OTP-02 hourly limit; process-local like the codes, case-insensitive.
    /// </summary>
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _sends =
        new(StringComparer.OrdinalIgnoreCase);
}
