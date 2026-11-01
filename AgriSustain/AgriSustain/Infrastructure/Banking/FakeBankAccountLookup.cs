using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Banking;

/// <summary>
/// Development stand-in for the VietQR bank account lookup (Report 3, external service 4).
/// Rules, so every alternative flow can be demonstrated:
///   - account number ending in 0000 → NotFound (MSG-BA-04);
///   - account number ending in 9999 → Unavailable (save as Unverified, BR-BA-06);
///   - an account already saved by any user → that saved holder name;
///   - anything else → a fixed demo holder name.
/// Replace with a real VietQR client before going live.
/// </summary>
public sealed class FakeBankAccountLookup(AgriSustainContext db) : IBankAccountLookup
{
    public const string DemoHolder = "NGUYEN VAN AGRI";

    public async Task<BankLookupResponse> LookupAsync(
        string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        // Behave like a network call so the 600 ms debounce on the screen is visible.
        await Task.Delay(300, cancellationToken);

        if (accountNumber.EndsWith("0000", StringComparison.Ordinal))
        {
            return new BankLookupResponse(BankLookupStatuses.NotFound, null);
        }

        if (accountNumber.EndsWith("9999", StringComparison.Ordinal))
        {
            return new BankLookupResponse(BankLookupStatuses.Unavailable, null);
        }

        string? saved = await db.UserBankAccounts.AsNoTracking()
            .Where(a => a.BankCode == bankCode && a.AccountNumber == accountNumber)
            .Select(a => a.AccountHolder)
            .FirstOrDefaultAsync(cancellationToken);

        return new BankLookupResponse(BankLookupStatuses.Found, saved ?? DemoHolder);
    }
}
