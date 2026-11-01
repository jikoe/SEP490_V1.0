using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Repositories;

/// <summary>UC-AUTH-03 Manage Bank Account: the user's default bank account (BR-BA-02).</summary>
public interface IBankAccountRepository
{
    Task<BankAccountOwner?> GetOwnerAsync(long userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupportedBankDto>> GetActiveBanksAsync(CancellationToken cancellationToken);

    Task<BankAccountDto?> GetDefaultAccountAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the default account or replaces it. When <paramref name="payoutHoldUntil"/> is set the
    /// payout hold is applied (BR-BA-05). Writes an AuditLog entry with the masked number.
    /// </summary>
    Task SaveDefaultAccountAsync(
        long userId,
        string bankCode,
        string accountNumber,
        string accountHolder,
        string verificationStatus,
        DateTime now,
        DateTime? payoutHoldUntil,
        CancellationToken cancellationToken);

    /// <summary>Deletes only the default account and audits it; refund/payout snapshots stay.</summary>
    Task<bool> RemoveDefaultAccountAsync(long userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>The bank and account number currently saved, or null (used to detect a change).</summary>
    Task<(string BankCode, string AccountNumber)?> GetDefaultAccountKeyAsync(
        long userId, CancellationToken cancellationToken);
}
