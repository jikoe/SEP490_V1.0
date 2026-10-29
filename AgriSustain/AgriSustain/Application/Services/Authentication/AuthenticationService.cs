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
    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxFailedAttempts = 5;

    private static readonly Error s_emailDeliveryFailed = new(
        "EMAIL_DELIVERY_FAILED", "Không thể gửi mã xác thực. Vui lòng thử lại sau.",
        "ServiceUnavailable");

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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

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
