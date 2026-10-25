namespace AgriSustain.Application.DTOs.Administration;

/// <summary>Status is the displayed account status; VerificationStatus is null for roles without KYC.</summary>
public sealed record UserDto(long Id, string Name, string? OrganizationName, string Email,
    string? Phone, string Role, string Status, string? VerificationStatus, DateTimeOffset CreatedAt,
    bool IsLocked, DateTimeOffset? LockedUntil, int FailedLoginCount, bool CanSuspend,
    long CurrentActorId);
