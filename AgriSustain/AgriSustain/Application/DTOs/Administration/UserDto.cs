namespace AgriSustain.Application.DTOs.Administration;

public sealed record UserDto(long Id, string Name, string? OrganizationName, string Email,
    string? Phone, string Role, string Status, string VerificationStatus, DateTimeOffset CreatedAt,
    bool IsLocked, DateTimeOffset? LockedUntil, int FailedLoginCount, long CurrentActorId);
