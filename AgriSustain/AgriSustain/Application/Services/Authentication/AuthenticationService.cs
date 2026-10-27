using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Authentication;

/// <summary>
/// UC-AUTH-01 Register RetailCustomer / Farmer account, including UC-AUTH-08 Verify Email OTP.
/// </summary>
public sealed class AuthenticationService(
    IUnitOfWork unitOfWork,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyOtpRequest> verifyOtpValidator,
    IValidator<ResendOtpRequest> resendOtpValidator,
    IEmailService emailService,
    IOtpGenerator otpGenerator,
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
        var now = timeProvider.GetUtcNow();

        if (await DuplicateErrorAsync(unitOfWork.Auth, email, phone, cancellationToken) is { } duplicate)
        {
            return Result<string>.FailureResult(duplicate);
        }

        string otp = otpGenerator.Generate();
        var pending = new PendingRegistration(
            Email: email,
            FullName: request.FullName.Trim(),
            PhoneNumber: phone,
            PasswordHash: BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role: request.Role,
            OtpCode: otp,
            OtpExpiresAt: now.Add(OtpLifetime),
            AttemptCount: 0,
            CreatedAt: now,
            OtpSentAt: now);

        // Send first: when the e-mail cannot be delivered no pending registration is kept (SRS AF Step 1).
        if (!await emailService.SendOtpEmailAsync(email, otp, cancellationToken))
        {
            return Result<string>.FailureResult(s_emailDeliveryFailed);
        }

        await unitOfWork.Auth.SavePendingRegistrationAsync(pending, cancellationToken);
        return Result<string>.SuccessResult($"Mã xác thực đã được gửi tới {email}. Mã có hiệu lực trong 5 phút.");
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

        if (pending.AttemptCount >= MaxFailedAttempts)
        {
            await unitOfWork.Auth.RemovePendingRegistrationAsync(email, cancellationToken);
            return Result<AuthResponse>.FailureResult(new Error(
                "MAX_ATTEMPTS_EXCEEDED",
                "Bạn đã nhập sai mã OTP quá 5 lần. Mã hiện tại đã bị vô hiệu, vui lòng đăng ký lại để nhận mã mới."));
        }

        if (timeProvider.GetUtcNow() > pending.OtpExpiresAt)
        {
            await unitOfWork.Auth.RemovePendingRegistrationAsync(email, cancellationToken);
            return Result<AuthResponse>.FailureResult(new Error(
                "OTP_EXPIRED", "Mã OTP đã hết hiệu lực. Vui lòng đăng ký lại để nhận mã mới."));
        }

        if (!OtpEquals(pending.OtpCode, request.OtpCode))
        {
            int attempts = pending.AttemptCount + 1;
            await unitOfWork.Auth.UpdatePendingRegistrationAsync(
                pending with { AttemptCount = attempts }, cancellationToken);
            int remaining = Math.Max(0, MaxFailedAttempts - attempts);
            return Result<AuthResponse>.FailureResult(new Error(
                "INVALID_OTP", $"Mã OTP không chính xác. Bạn còn {remaining} lần thử."));
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

        // The OTP is single-use: it is invalidated once the account is created (or can no longer be).
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
        PendingRegistration? pending = await unitOfWork.Auth.FindPendingRegistrationAsync(email, cancellationToken);
        if (pending == null)
        {
            return Result<string>.FailureResult(new Error(
                "NOT_FOUND", "Không tìm thấy phiên đăng ký cho email này. Vui lòng đăng ký lại.", "NotFound"));
        }

        var now = timeProvider.GetUtcNow();
        var wait = pending.OtpSentAt.Add(ResendCooldown) - now;
        if (wait > TimeSpan.Zero)
        {
            return Result<string>.FailureResult(new Error(
                "RESEND_COOLDOWN",
                $"Vui lòng chờ {Math.Ceiling(wait.TotalSeconds)} giây trước khi gửi lại mã.", "TooManyRequests"));
        }

        string newOtp = otpGenerator.Generate();
        if (!await emailService.SendOtpEmailAsync(email, newOtp, cancellationToken))
        {
            return Result<string>.FailureResult(s_emailDeliveryFailed);
        }

        await unitOfWork.Auth.UpdatePendingRegistrationAsync(pending with
        {
            OtpCode = newOtp,
            OtpExpiresAt = now.Add(OtpLifetime),
            OtpSentAt = now,
            AttemptCount = 0
        }, cancellationToken);

        return Result<string>.SuccessResult($"Mã xác thực đã được gửi tới {email}. Mã có hiệu lực trong 5 phút.");
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool OtpEquals(string expected, string actual) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(actual.Trim()));

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
