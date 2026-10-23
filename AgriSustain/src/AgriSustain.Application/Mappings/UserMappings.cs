using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Mappings;

public static class UserMappings
{
    /// <summary>
    /// Projects only permitted account/profile and lock fields, evaluating the lock at the
    /// supplied instant. No SQL concurrency Version or authentication secret is exposed.
    /// </summary>
    public static UserResponse ToResponse(UserAccount user, DateTimeOffset now) => new(
        user.Id, user.Name, user.Email, user.Phone, user.Role, user.Status,
        user.OrganizationName, user.VerificationStatus, user.CreatedAt,
        user.LockedUntil, user.FailedLoginCount, user.IsTemporarilyLocked(now));
}
