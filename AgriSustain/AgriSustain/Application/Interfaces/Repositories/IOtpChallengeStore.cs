using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Repositories;

/// <summary>Keeps the active UC-AUTH-08 code per purpose and e-mail address.</summary>
public interface IOtpChallengeStore
{
    Task<OtpChallenge?> FindAsync(string purpose, string email, CancellationToken cancellationToken);

    Task SaveAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    /// <summary>Replaces <paramref name="current"/> only if nobody changed it meanwhile.</summary>
    Task<bool> TryReplaceAsync(
        OtpChallenge current, OtpChallenge next, CancellationToken cancellationToken);

    /// <summary>Consumes exactly this code; false when another request already used it.</summary>
    Task<bool> TryRemoveAsync(OtpChallenge challenge, CancellationToken cancellationToken);

    Task RemoveAsync(string purpose, string email, CancellationToken cancellationToken);

    /// <summary>
    /// BR-OTP-02 hourly limit: records a delivered code for the address (all purposes) and
    /// returns the send times still inside the window, oldest first, including this one.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> RecordSendAsync(string email, DateTimeOffset sentAt,
        TimeSpan window, CancellationToken cancellationToken);

    /// <summary>Send times of the address inside the window, oldest first.</summary>
    Task<IReadOnlyList<DateTimeOffset>> RecentSendsAsync(string email, DateTimeOffset now,
        TimeSpan window, CancellationToken cancellationToken);
}
