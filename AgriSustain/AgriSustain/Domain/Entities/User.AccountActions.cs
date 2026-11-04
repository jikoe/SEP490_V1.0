namespace AgriSustain.Domain.Entities;

public partial class User
{
    public bool CanBeManagedBy(User actor)
    {
        return actor.UserId != UserId && actor.Status == "Active"
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

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
    public bool AllowsAccountAction(string action, DateTimeOffset now) => action switch
    {
        "suspend" => CanBeSuspended && AccountStatus == "Active",
        "activate" => CanBeSuspended && Status == "Suspended",
        "unlock" => AccountStatus == "Active" && IsTemporarilyLocked(now),
        _ => false
    };

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

    /// <summary>BR-UM-08: lifts only the suspension; KYC state and an unexpired lock stay as they are.</summary>
    public void Activate(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != "Suspended")
        {
            throw new InvalidOperationException("Only suspended accounts can be activated.");
        }
        Status = "Active";
    }

    public void Unlock()
    {
        LockedUntil = null;
        FailedLoginCount = 0;
    }

    private static string StatusFromKyc(string? verificationStatus) => verificationStatus switch
    {
        "Approved" => "Active",
        "Rejected" => "Rejected",
        _ => "Pending"
    };
}
