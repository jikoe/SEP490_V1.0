namespace AgriSustain.Domain.Entities;

public partial class User
{
    /// <summary>
    /// An Administrator may act on any account except their own (MSG-UM-03).
    /// </summary>
    /// <param name="actor">Caller.</param>
    /// <returns>True when the caller may change this account.</returns>
    public bool CanBeManagedBy(User actor)
    {
        return actor.UserId != UserId && actor.Status == "Active"
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    /// <summary>
    /// True while the login lock (after repeated failed logins) has not expired.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>True when locked.</returns>
    public bool IsTemporarilyLocked(DateTimeOffset now)
    {
        return LockedUntil > now.UtcDateTime;
    }

    /// <summary>Only marketplace accounts can be suspended; staff and admin accounts stay active.</summary>
    public bool CanBeSuspended => Role is "Farmer" or "WholesaleBuyer" or "RetailCustomer";

    /// <summary>
    /// BR-UM-01 displayed status: Farmer/WholesaleBuyer stay Pending until KYC is Approved
    /// (Rejected when declined); other accounts are Active unless suspended. Needs the KYC
    /// profiles loaded, and mirrors the SQL projection in AdministrationRepository.
    /// </summary>
    public string AccountStatus => Status == "Suspended" ? "Suspended" : Role switch
    {
        "Farmer" => StatusFromKyc(FarmerProfile?.VerificationStatus),
        "WholesaleBuyer" => StatusFromKyc(WholesaleBuyerProfile?.VerificationStatus),
        _ => "Active"
    };

    /// <summary>
    /// BR-UM-02 and BR-UM-05: Suspend an Active marketplace account, Reactivate a Suspended one,
    /// Unlock any Active account with an unexpired lock. Pending and Rejected accounts get nothing.
    /// </summary>
    /// <param name="action">suspend, activate or unlock.</param>
    /// <param name="now">Current time.</param>
    /// <returns>True when the action is allowed now.</returns>
    public bool AllowsAccountAction(string action, DateTimeOffset now) => action switch
    {
        "suspend" => CanBeSuspended && AccountStatus == "Active",
        "activate" => CanBeSuspended && Status == "Suspended",
        "unlock" => AccountStatus == "Active" && IsTemporarilyLocked(now),
        _ => false
    };

    /// <summary>
    /// Active marketplace account → Suspended. Throws when not allowed; the service checks first.
    /// </summary>
    /// <param name="reason">Required reason.</param>
    public void Suspend(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!CanBeSuspended)
        {
            throw new InvalidOperationException("Only marketplace accounts can be suspended.");
        }
        if (Status != "Active")
        {
            throw new InvalidOperationException("Only active accounts can be suspended.");
        }
        Status = "Suspended";
    }

    /// <summary>
    /// BR-UM-08: lifts only the suspension; KYC state and an unexpired lock stay as they are.
    /// </summary>
    /// <param name="reason">Required reason.</param>
    public void Activate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != "Suspended")
        {
            throw new InvalidOperationException("Only suspended accounts can be activated.");
        }
        Status = "Active";
    }

    /// <summary>
    /// BR-UM-05: clears the login lock and the failed login counter.
    /// </summary>
    public void Unlock()
    {
        LockedUntil = null;
        FailedLoginCount = 0;
    }

    /// <summary>
    /// Account status from a KYC result: Approved → Active, Rejected → Rejected, otherwise Pending.
    /// </summary>
    /// <param name="verificationStatus">KYC status, or null when not submitted.</param>
    /// <returns>Displayed status.</returns>
    private static string StatusFromKyc(string? verificationStatus) => verificationStatus switch
    {
        "Approved" => "Active",
        "Rejected" => "Rejected",
        _ => "Pending"
    };
}
