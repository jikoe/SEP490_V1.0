namespace AgriSustain.Application.DTOs.Authentication;

/// <summary>UC-AUTH-07 step 1 and resend: the registered e-mail address.</summary>
public sealed record ForgotPasswordRequest(
    string Email);

/// <summary>
/// Returned after UC-AUTH-08 succeeds; the token authorizes exactly one password change for
/// <see cref="Email"/> until <see cref="ExpiresAt"/>.
/// </summary>
public sealed record PasswordResetGrantResponse(
    string Email,
    string ResetToken,
    DateTimeOffset ExpiresAt);

/// <summary>UC-AUTH-07 step 3: the new password, authorized by the reset token.</summary>
public sealed record ResetPasswordRequest(
    string Email,
    string ResetToken,
    string NewPassword,
    string ConfirmPassword);
