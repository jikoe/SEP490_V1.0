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

    public void Suspend(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != "Active")
        {
            throw new InvalidOperationException("Only active accounts can be suspended.");
        }
        Status = "Suspended";
    }

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
}
