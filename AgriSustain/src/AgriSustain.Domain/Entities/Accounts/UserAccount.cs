using AgriSustain.Domain.Exceptions;

namespace AgriSustain.Domain.Entities.Accounts;

public sealed class UserAccount
{
    public long Id
    {
        get; init;
    }
    public required string Name
    {
        get; init;
    }
    public required string Email
    {
        get; init;
    }
    public string? Phone
    {
        get; init;
    }
    public required string Role
    {
        get; init;
    }
    public string? OrganizationName
    {
        get; init;
    }
    public string? VerificationStatus
    {
        get; init;
    }
    public DateTimeOffset CreatedAt
    {
        get; init;
    }
    public string Status { get; private set; } = "Active";
    public DateTimeOffset? LockedUntil
    {
        get; private set;
    }
    public int FailedLoginCount
    {
        get; private set;
    }
    public int SessionVersion
    {
        get; private set;
    }

    /// <summary>
    /// Evaluates LockedUntil against the supplied server instant; does not confuse temporary
    /// login lockout with permanent account suspension.
    /// </summary>
    public bool IsTemporarilyLocked(DateTimeOffset now) => LockedUntil > now;

    /// <summary>
    /// Allows only an active account with a reason to become suspended, invalidating the original
    /// mock session. The auth module must revoke real sessions separately.
    /// </summary>
    public void Suspend(string reason)
    {
        if (Status != "Active" || string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Tài khoản không đủ điều kiện tạm ngưng.");
        }

        Status = "Suspended";
        SessionVersion++;
    }

    /// <summary>
    /// Requires a suspended account and a reason, then restores Active. It intentionally does not
    /// restore a previously revoked mock authentication session.
    /// </summary>
    public void Activate(string reason)
    {
        if (Status != "Suspended" || string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Tài khoản không đủ điều kiện kích hoạt lại.");
        }

        Status = "Active";
    }

    /// <summary>
    /// Clears a currently active temporary lock and failed-login counter while preserving the
    /// account business status. Rejects an account that is not temporarily locked.
    /// </summary>
    public void Unlock(DateTimeOffset now)
    {
        if (!IsTemporarilyLocked(now))
        {
            throw new BusinessRuleException("Tài khoản không đang bị khóa tạm.");
        }

        LockedUntil = null;
        FailedLoginCount = 0;
    }

    // The authentication module owns lockout policy. This factory only restores its state.
    /// <summary>
    /// Rehydrates lock metadata owned by the authentication module into a copied fixture account.
    /// It does not implement login attempt counting or lockout policy.
    /// </summary>
    public static UserAccount RestoreLock(UserAccount account, DateTimeOffset until, int count)
    {
        var restored = account.Copy();
        restored.LockedUntil = until;
        restored.FailedLoginCount = count;
        return restored;
    }

    /// <summary>
    /// Detaches account status and lock/session metadata for copy-on-write rollback; no
    /// credential material is present in this entity.
    /// </summary>
    public UserAccount Copy() => (UserAccount)MemberwiseClone();

    /// <summary>
    /// Restores trusted persisted business/lock state without executing a transition or audit.
    /// Session version remains an external mock projection, never an assumed dbo.User column.
    /// </summary>
    public static UserAccount RestorePersisted(
        UserAccount account, string status, DateTimeOffset? lockedUntil,
        int failedCount, int mockSessionVersion)
    {
        var restored = account.Copy();
        restored.Status = status;
        restored.LockedUntil = lockedUntil;
        restored.FailedLoginCount = failedCount;
        restored.SessionVersion = mockSessionVersion;
        return restored;
    }
}
