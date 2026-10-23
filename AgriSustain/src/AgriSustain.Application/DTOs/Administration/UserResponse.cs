namespace AgriSustain.Application.DTOs.Administration;

public sealed record UserResponse(
    long Id, string Name, string Email, string? Phone, string Role, string Status,
    string? OrganizationName, string? VerificationStatus, DateTimeOffset CreatedAt,
    DateTimeOffset? LockedUntil, int FailedLoginCount, bool IsLocked);
