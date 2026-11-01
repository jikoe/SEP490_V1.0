namespace AgriSustain.Application.DTOs.Profiles;

// UC-AUTH-03 Manage Profile - Manage Bank Account (BR-BA-01..06).

/// <summary>A bank the user can choose (BR-BA-01: active supported list).</summary>
public sealed record SupportedBankDto(
    string BankCode,
    string ShortName,
    string FullName);

/// <summary>The saved default account. The number is always masked (BR-BA-04).</summary>
public sealed record BankAccountDto(
    string BankCode,
    string BankShortName,
    string BankFullName,
    string MaskedAccountNumber,
    string Last4,
    string AccountHolder,
    string VerificationStatus,
    DateTime? PayoutHoldUntil,
    string? QrImageUrl,
    DateTime? UpdatedAt);

/// <summary>Content of the Bank Account tab.</summary>
public sealed record BankAccountOverview(
    string Role,
    string Purpose,
    bool CanRemove,
    BankAccountDto? Account,
    IReadOnlyList<SupportedBankDto> Banks);

/// <summary>Step 3: look up the holder name at the bank.</summary>
public sealed record BankLookupRequest(
    string BankCode,
    string AccountNumber);

public static class BankLookupStatuses
{
    public const string Found = "Found";
    public const string NotFound = "NotFound";
    public const string Unavailable = "Unavailable";
}

/// <summary>Status is Found, NotFound or Unavailable; AccountHolder is set only when Found.</summary>
public sealed record BankLookupResponse(
    string Status,
    string? AccountHolder);

/// <summary>
/// Steps 4-6: save the account as the default account. AccountHolder is used only when the bank
/// lookup is unavailable (save as Unverified, BR-BA-06); otherwise the looked-up name is stored.
/// </summary>
public sealed record SaveBankAccountRequest(
    string BankCode,
    string AccountNumber,
    string? AccountHolder,
    bool ConfirmRecipient,
    string OtpCode);

/// <summary>Alternative flow "Remove account": confirmed with an e-mail OTP (BR-BA-04).</summary>
public sealed record RemoveBankAccountRequest(
    string OtpCode);

/// <summary>Result of a save or remove: the refreshed tab content and the toast text.</summary>
public sealed record BankAccountChangeResponse(
    string Message,
    BankAccountOverview Overview);

/// <summary>Row data needed by the service; never sent to the client unmasked.</summary>
public sealed record BankAccountOwner(
    long UserId,
    string Role,
    string Email,
    string FullName,
    string? FarmVerificationStatus);
