using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Services;

public interface IAuthenticationService
{
    /// <summary>
    /// UC-AUTH-01 step 1. Validates the form (name 2–50, phone, e-mail ≤ 255, strong password and its confirmation, role
    /// RetailCustomer or Farmer, terms accepted), refuses an e-mail or phone already used, then issues the Registration
    /// code through UC-AUTH-08. Only when the e-mail was delivered is the registration kept as pending (password stored
    /// as a BCrypt hash); no account exists yet.
    /// </summary>
    /// <param name="request">Registration form.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14; 400 / 409 EMAIL_EXISTS or PHONE_EXISTS; 429; 503 EMAIL_DELIVERY_FAILED.</returns>
    Task<Result<string>> InitiateRegistrationAsync(
        RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// UC-AUTH-01 step 2. Finds the pending registration (REGISTRATION_EXPIRED when none), verifies the code through
    /// UC-AUTH-08, then in one unit of work re-checks that the e-mail and phone are still free and creates the user. The
    /// pending data is removed once the account exists or can never be created (duplicate). The user signs in afterwards;
    /// no token is returned.
    /// </summary>
    /// <param name="request">E-mail and 6-digit code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The new account; or 400 / 404 / 409.</returns>
    Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resends the Registration code while a pending registration exists (NOT_FOUND otherwise).
    /// </summary>
    /// <param name="request">E-mail.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14, or the cooldown / limit / delivery / not-found error.</returns>
    Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken);
}
