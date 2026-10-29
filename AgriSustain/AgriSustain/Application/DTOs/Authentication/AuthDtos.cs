namespace AgriSustain.Application.DTOs.Authentication;

public sealed record RegisterRequest(
    string FullName,
    string PhoneNumber,
    string Email,
    string Password,
    string ConfirmPassword,
    string Role,
    bool AgreeTerms);

public sealed record VerifyOtpRequest(
    string Email,
    string OtpCode);

public sealed record ResendOtpRequest(
    string Email);

public sealed record AuthResponse(
    long UserId,
    string FullName,
    string Email,
    string Role,
    string? Token,
    string Message);

/// <summary>Registration data kept until UC-AUTH-08 confirms the e-mail address.</summary>
public sealed record PendingRegistration(
    string Email,
    string FullName,
    string PhoneNumber,
    string PasswordHash,
    string Role,
    DateTimeOffset CreatedAt);

/// <summary>Calling use cases of UC-AUTH-08; each purpose has its own code per e-mail.</summary>
public static class OtpPurposes
{
    public const string Registration = "Registration";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordResetGrant = "PasswordResetGrant";
}

/// <summary>
/// One single-use code issued to an e-mail address. A code is no longer usable once it expires
/// or reaches the failed-attempt limit; only a resend replaces it.
/// </summary>
public sealed record OtpChallenge(
    string Purpose,
    string Email,
    string OtpCode,
    DateTimeOffset ExpiresAt,
    int AttemptCount,
    DateTimeOffset SentAt);
