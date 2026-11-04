namespace AgriSustain.Application.DTOs.Administration;

/// <summary>
/// ExpectedStatus and ExpectedLockedUntil are the account version shown at Step 4 (BR-UM-12);
/// the action is applied only while the account still matches them.
/// </summary>
public sealed record AccountActionRequest(string Action, string? Reason, bool Confirmed,
    string? ExpectedStatus = null, DateTimeOffset? ExpectedLockedUntil = null);

/// <summary>Step 6 impact shown with MSG-UM-07 before a Suspend is confirmed.</summary>
public sealed record AccountImpactDto(int OpenOrders, int ActiveContracts, decimal HeldPayoutAmount)
{
    public bool HasOpenTransactions => OpenOrders > 0 || ActiveContracts > 0 || HeldPayoutAmount > 0;
}

/// <summary>Message is MSG-UM-08, or MSG-UM-09 when the account email is queued for retry.</summary>
public sealed record AccountActionResultDto(UserDto Account, string Message, bool EmailSent);
