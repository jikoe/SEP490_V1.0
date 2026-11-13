using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Repositories;

/// <summary>Keeps the active UC-AUTH-08 code per purpose and e-mail address.</summary>
public interface IOtpChallengeStore
{
    /// <summary>
    /// Current code of the purpose and address.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The code, or null.</returns>
    Task<OtpChallenge?> FindAsync(string purpose, string email, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a code, replacing any earlier one of the same purpose and address.
    /// </summary>
    /// <param name="challenge">Code to store.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces <paramref name="current"/> only if nobody changed it meanwhile.
    /// </summary>
    /// <param name="current">Code as read.</param>
    /// <param name="next">New value.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>False when it changed in between.</returns>
    Task<bool> TryReplaceAsync(
        OtpChallenge current, OtpChallenge next, CancellationToken cancellationToken);

    /// <summary>
    /// Consumes exactly this code; false when another request already used it.
    /// </summary>
    /// <param name="challenge">Code as read.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when removed now.</returns>
    Task<bool> TryRemoveAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the code of the purpose and address, if any.
    /// </summary>
    /// <param name="purpose">Calling use case: Registration, PasswordReset or BankAccount; each purpose has its own code.</param>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(string purpose, string email, CancellationToken cancellationToken);

    /// <summary>
    /// BR-OTP-02 hourly limit: records a delivered code for the address (all purposes) and
    /// returns the send times still inside the window, oldest first, including this one.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="sentAt">Delivery time.</param>
    /// <param name="window">Rolling window.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Send times inside the window.</returns>
    Task<IReadOnlyList<DateTimeOffset>> RecordSendAsync(string email, DateTimeOffset sentAt,
        TimeSpan window, CancellationToken cancellationToken);

    /// <summary>
    /// Send times of the address inside the window, oldest first.
    /// </summary>
    /// <param name="email">Normalised e-mail address (the caller trims and lower-cases it).</param>
    /// <param name="now">Current time.</param>
    /// <param name="window">Rolling window.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Send times.</returns>
    Task<IReadOnlyList<DateTimeOffset>> RecentSendsAsync(string email, DateTimeOffset now,
        TimeSpan window, CancellationToken cancellationToken);
}
