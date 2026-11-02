using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Services;

/// <summary>UC-AUTH-03 Manage Profile - Manage Bank Account. Users only manage their own account.</summary>
public interface IBankAccountService
{
    Task<Result<BankAccountOverview>> GetAsync(CancellationToken cancellationToken);

    Task<Result<BankLookupResponse>> LookupAsync(BankLookupRequest request, CancellationToken cancellationToken);

    /// <summary>Sends the 6-digit OTP to the account e-mail (BR-BA-04).</summary>
    Task<Result<string>> RequestOtpAsync(CancellationToken cancellationToken);

    Task<Result<BankAccountChangeResponse>> SaveAsync(
        SaveBankAccountRequest request, CancellationToken cancellationToken);

    Task<Result<BankAccountChangeResponse>> RemoveAsync(
        RemoveBankAccountRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Bank Account Lookup (VietQR): returns the holder name for a bank code and account number.
/// A fake provider is used in development.
/// </summary>
public interface IBankAccountLookup
{
    Task<BankLookupResponse> LookupAsync(
        string bankCode, string accountNumber, CancellationToken cancellationToken);
}

/// <summary>MSG-BA-05: e-mail sent to the account e-mail after a bank account is changed or removed.</summary>
public interface IBankAccountEmailSender
{
    Task<bool> SendBankAccountChangedAsync(
        string email, string? bankShortName, string? last4, DateTime changedAtUtc,
        CancellationToken cancellationToken);
}
