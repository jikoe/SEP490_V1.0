using System.Text.Json;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-AUTH-03 Manage Bank Account over dbo.UserBankAccount and dbo.SupportedBank.</summary>
public sealed class BankAccountRepository(AgriSustainContext db) : IBankAccountRepository
{
    public async Task<BankAccountOwner?> GetOwnerAsync(long userId, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new BankAccountOwner(
                u.UserId,
                u.Role,
                u.Email,
                u.FullName,
                u.FarmerProfile == null ? null : u.FarmerProfile.VerificationStatus))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SupportedBankDto>> GetActiveBanksAsync(CancellationToken cancellationToken) =>
        await db.SupportedBanks.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.ShortName)
            .Select(b => new SupportedBankDto(b.BankCode, b.ShortName, b.FullName))
            .ToListAsync(cancellationToken);

    public async Task<BankAccountDto?> GetDefaultAccountAsync(long userId, CancellationToken cancellationToken)
    {
        var row = await db.UserBankAccounts.AsNoTracking()
            .Where(a => a.UserId == userId && a.IsDefault)
            .Select(a => new
            {
                a.BankCode,
                a.BankCodeNavigation.ShortName,
                a.BankCodeNavigation.FullName,
                a.AccountNumber,
                a.AccountHolder,
                a.VerificationStatus,
                a.PayoutHoldUntil,
                a.QrImageUrl,
                a.UpdatedAt,
                a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row == null) return null;

        // Only the last 4 digits ever leave the server (BR-BA-04).
        string last4 = row.AccountNumber.Length <= 4 ? row.AccountNumber : row.AccountNumber[^4..];
        return new BankAccountDto(
            row.BankCode,
            row.ShortName,
            row.FullName,
            "•••• " + last4,
            last4,
            row.AccountHolder,
            row.VerificationStatus,
            row.PayoutHoldUntil,
            row.QrImageUrl,
            row.UpdatedAt ?? row.CreatedAt);
    }

    public async Task<(string BankCode, string AccountNumber)?> GetDefaultAccountKeyAsync(
        long userId, CancellationToken cancellationToken)
    {
        var row = await db.UserBankAccounts.AsNoTracking()
            .Where(a => a.UserId == userId && a.IsDefault)
            .Select(a => new { a.BankCode, a.AccountNumber })
            .FirstOrDefaultAsync(cancellationToken);
        return row == null ? null : (row.BankCode, row.AccountNumber);
    }

    public async Task SaveDefaultAccountAsync(
        long userId,
        string bankCode,
        string accountNumber,
        string accountHolder,
        string verificationStatus,
        DateTime now,
        DateTime? payoutHoldUntil,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // One row per user (User.UserBankAccount is one-to-one): replace it in place.
        var account = await db.UserBankAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        object? before = account == null ? null : Snapshot(account);
        string action = account == null ? "AddBankAccount" : "ChangeBankAccount";

        if (account == null)
        {
            account = new UserBankAccount { UserId = userId, CreatedAt = now };
            db.UserBankAccounts.Add(account);
        }
        else
        {
            account.UpdatedAt = now;
        }

        account.BankCode = bankCode;
        account.AccountNumber = accountNumber;
        account.AccountHolder = accountHolder;
        account.IsDefault = true;
        account.VerificationStatus = verificationStatus;
        account.VerifiedAt = verificationStatus == "Verified" ? now : null;
        if (payoutHoldUntil.HasValue)
        {
            account.PayoutHoldUntil = payoutHoldUntil;
        }

        // A QR image belongs to the previous account number; it must be added again.
        if (before != null)
        {
            account.QrImageUrl = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        AddAudit(userId, account.UserBankAccountId, action, before, Snapshot(account), now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> RemoveDefaultAccountAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        var account = await db.UserBankAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);
        if (account == null) return false;

        // Refunds and payouts keep their own copy of the account (BR-BA-02), so nothing else changes.
        AddAudit(userId, account.UserBankAccountId, "RemoveBankAccount", Snapshot(account), null, now);
        db.UserBankAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static object Snapshot(UserBankAccount account) => new
    {
        account.BankCode,
        AccountNumber = "•••• " + (account.AccountNumber.Length <= 4
            ? account.AccountNumber : account.AccountNumber[^4..]),
        account.AccountHolder,
        account.VerificationStatus,
        account.PayoutHoldUntil
    };

    private void AddAudit(long userId, long accountId, string action, object? before, object? after, DateTime now) =>
        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = "UserBankAccount",
            EntityId = accountId,
            Action = action,
            OldValue = before == null ? null : JsonSerializer.Serialize(before),
            NewValue = after == null ? null : JsonSerializer.Serialize(after),
            OccurredAt = now
        });
}
