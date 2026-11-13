using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Authentication;

/// <summary>
/// UC-AUTH-01 Register RetailCustomer / Farmer account; the e-mail is confirmed by including
/// UC-AUTH-08 Verify Email OTP (<see cref="IEmailOtpVerifier"/>).
/// </summary>
public sealed class AuthenticationService(
    IUnitOfWork unitOfWork,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyOtpRequest> verifyOtpValidator,
    IValidator<ResendOtpRequest> resendOtpValidator,
    IEmailOtpVerifier otpVerifier,
    TimeProvider timeProvider) : IAuthenticationService
{
    /// <summary>
    /// Not used here any more: the OTP rules live in <see cref="EmailOtpVerifier"/>.
    /// </summary>
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Not used here any more: see <see cref="EmailOtpVerifier.ResendCooldown"/>.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    /// <summary>
    /// Not used here any more: see <see cref="EmailOtpVerifier.MaxFailedAttempts"/>.
    /// </summary>
    public const int MaxFailedAttempts = 5;

    private static readonly Error s_emailDeliveryFailed = new(
        "EMAIL_DELIVERY_FAILED", "Không thể gửi mã xác thực. Vui lòng thử lại sau.",
        "ServiceUnavailable");

    /// <summary>
    /// UC-AUTH-01 step 1. Validates the form (name 2–50, phone, e-mail ≤ 255, strong password and its confirmation, role
    /// RetailCustomer or Farmer, terms accepted), refuses an e-mail or phone already used, then issues the Registration
    /// code through UC-AUTH-08. Only when the e-mail was delivered is the registration kept as pending (password stored
    /// as a BCrypt hash); no account exists yet.
    /// </summary>
    /// <param name="request">Registration form.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14; 400 / 409 EMAIL_EXISTS or PHONE_EXISTS; 429; 503 EMAIL_DELIVERY_FAILED.</returns>
    public async Task<Result<string>> InitiateRegistrationAsync(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(registerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        string phone = request.PhoneNumber.Trim();

        if (await DuplicateErrorAsync(unitOfWork.Auth, email, phone, cancellationToken) is { } duplicate)
        {
            return Result<string>.FailureResult(duplicate);
        }

        // When the e-mail cannot be delivered no pending registration is kept (SRS AF Step 1).
        var issued = await otpVerifier.IssueAsync(OtpPurposes.Registration, email, cancellationToken);
        if (!issued.IsSuccess)
        {
            return issued;
        }

        await unitOfWork.Auth.SavePendingRegistrationAsync(new PendingRegistration(
            Email: email,
            FullName: request.FullName.Trim(),
            PhoneNumber: phone,
            PasswordHash: BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role: request.Role,
            CreatedAt: timeProvider.GetUtcNow()), cancellationToken);
        return issued;
    }

    /// <summary>
    /// UC-AUTH-01 step 2. Finds the pending registration (REGISTRATION_EXPIRED when none), verifies the code through
    /// UC-AUTH-08, then in one unit of work re-checks that the e-mail and phone are still free and creates the user. The
    /// pending data is removed once the account exists or can never be created (duplicate). The user signs in afterwards;
    /// no token is returned.
    /// </summary>
    /// <param name="request">E-mail and 6-digit code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The new account; or 400 / 404 / 409.</returns>
    public async Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(verifyOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<AuthResponse>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        PendingRegistration? pending = await unitOfWork.Auth.FindPendingRegistrationAsync(email, cancellationToken);
        if (pending == null)
        {
            return Result<AuthResponse>.FailureResult(new Error(
                "REGISTRATION_EXPIRED",
                "Yêu cầu đăng ký không tồn tại hoặc đã hết hạn. Vui lòng đăng ký lại.", "NotFound"));
        }

        var verified = await otpVerifier.VerifyAsync(
            OtpPurposes.Registration, email, request.OtpCode, cancellationToken);
        if (!verified.IsSuccess)
        {
            return Result<AuthResponse>.FailureResult(verified.Error!);
        }

        var created = await unitOfWork.ExecuteAsync(async uow =>
        {
            // Re-check uniqueness: another account may have taken the email/phone while the OTP was pending.
            if (await DuplicateErrorAsync(uow.Auth, pending.Email, pending.PhoneNumber, cancellationToken) is { } dup)
            {
                return Result<AuthResponse>.FailureResult(dup);
            }

            var user = await uow.Auth.CreateUserFromPendingAsync(pending, cancellationToken);
            return Result<AuthResponse>.SuccessResult(new AuthResponse(
                UserId: user.UserId,
                FullName: user.FullName,
                Email: user.Email,
                Role: user.Role,
                Token: null,
                Message: "Đăng ký thành công. Vui lòng đăng nhập để tiếp tục."));
        }, cancellationToken);

        // The OTP was consumed above; the pending data is dropped once the account exists or never can.
        if (created.IsSuccess || created.Error?.Kind == "Conflict")
        {
            await unitOfWork.Auth.RemovePendingRegistrationAsync(email, cancellationToken);
        }

        return created;
    }

    /// <summary>
    /// Resends the Registration code while a pending registration exists (NOT_FOUND otherwise).
    /// </summary>
    /// <param name="request">E-mail.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG14, or the cooldown / limit / delivery / not-found error.</returns>
    public async Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(resendOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        string email = NormalizeEmail(request.Email);
        if (await unitOfWork.Auth.FindPendingRegistrationAsync(email, cancellationToken) == null)
        {
            return Result<string>.FailureResult(new Error(
                "NOT_FOUND", "Không tìm thấy phiên đăng ký cho email này. Vui lòng đăng ký lại.", "NotFound"));
        }

        return await otpVerifier.ResendAsync(OtpPurposes.Registration, email, cancellationToken);
    }

    /// <summary>
    /// Trims and lower-cases the e-mail.
    /// </summary>
    /// <param name="email">E-mail as typed.</param>
    /// <returns>Normalised e-mail.</returns>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// EMAIL_EXISTS or PHONE_EXISTS (409, on the field) when an account already uses them.
    /// </summary>
    /// <param name="auth">Auth repository of the current unit of work.</param>
    /// <param name="email">Normalised e-mail.</param>
    /// <param name="phone">Trimmed phone.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The error, or null when both are free.</returns>
    private static async Task<Error?> DuplicateErrorAsync(
        IAuthRepository auth, string email, string phone, CancellationToken cancellationToken)
    {
        if (await auth.EmailExistsAsync(email, cancellationToken))
        {
            return new Error("EMAIL_EXISTS", "Email này đã được sử dụng bởi một tài khoản khác.", "Conflict",
                new Dictionary<string, string[]> { ["email"] = ["Email này đã được sử dụng."] });
        }

        if (await auth.PhoneExistsAsync(phone, cancellationToken))
        {
            return new Error("PHONE_EXISTS", "Số điện thoại này đã được sử dụng.", "Conflict",
                new Dictionary<string, string[]> { ["phoneNumber"] = ["Số điện thoại này đã được sử dụng."] });
        }

        return null;
    }
}
