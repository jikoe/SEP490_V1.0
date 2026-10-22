using System.Security.Cryptography;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Authentication;

public sealed class AuthenticationService(
    IThreeUcUnitOfWork unitOfWork,
    IValidator<RegisterRequest> registerValidator,
    IValidator<VerifyOtpRequest> verifyOtpValidator,
    IValidator<ResendOtpRequest> resendOtpValidator) : IAuthenticationService
{
    public async Task<Result<string>> InitiateRegistrationAsync(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(registerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            if (await session.Auth.EmailExistsAsync(request.Email, cancellationToken))
            {
                return Result<string>.FailureResult(new Error(
                    "EMAIL_EXISTS", "Email này đã được sử dụng bởi một tài khoản khác.", "Conflict"));
            }

            if (await session.Auth.PhoneExistsAsync(request.PhoneNumber, cancellationToken))
            {
                return Result<string>.FailureResult(new Error(
                    "PHONE_EXISTS", "Số điện thoại này đã được sử dụng.", "Conflict"));
            }

            string otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            string hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var pending = new PendingRegistration(
                Email: request.Email,
                FullName: request.FullName,
                PhoneNumber: request.PhoneNumber,
                PasswordHash: hash,
                Role: request.Role,
                OtpCode: otp,
                OtpExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptCount: 0,
                CreatedAt: DateTimeOffset.UtcNow);

            await session.Auth.SavePendingRegistrationAsync(pending, cancellationToken);

            return Result<string>.SuccessResult($"Mã xác thực OTP đã được gửi đến {request.Email}. Mã có hiệu lực trong 5 phút. [Demo OTP: {otp}]");
        }, cancellationToken);
    }

    public async Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(verifyOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<AuthResponse>.FailureResult(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            PendingRegistration? pending = await session.Auth.FindPendingRegistrationAsync(request.Email, cancellationToken);
            if (pending == null)
            {
                return Result<AuthResponse>.FailureResult(new Error(
                    "REGISTRATION_EXPIRED", "Yêu cầu đăng ký không tồn tại hoặc đã hết hạn. Vui lòng đăng ký lại.", "NotFound"));
            }

            if (DateTimeOffset.UtcNow > pending.OtpExpiresAt)
            {
                await session.Auth.RemovePendingRegistrationAsync(request.Email, cancellationToken);
                return Result<AuthResponse>.FailureResult(new Error(
                    "OTP_EXPIRED", "Mã OTP đã hết hiệu lực. Vui lòng yêu cầu gửi lại mã mới.", "Validation"));
            }

            if (pending.OtpCode != request.OtpCode)
            {
                if (pending.AttemptCount >= 4)
                {
                    await session.Auth.RemovePendingRegistrationAsync(request.Email, cancellationToken);
                    return Result<AuthResponse>.FailureResult(new Error(
                        "MAX_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá 5 lần. Yêu cầu đăng ký đã bị hủy vì lý do bảo mật.", "Validation"));
                }

                await session.Auth.UpdatePendingRegistrationAsync(
                    pending with { AttemptCount = pending.AttemptCount + 1 }, cancellationToken);

                return Result<AuthResponse>.FailureResult(new Error(
                    "INVALID_OTP", $"Mã OTP không chính xác. Bạn còn {4 - pending.AttemptCount} lần thử.", "Validation"));
            }

            var user = await session.Auth.CreateUserFromPendingAsync(pending, cancellationToken);
            await session.Auth.RemovePendingRegistrationAsync(request.Email, cancellationToken);

            var response = new AuthResponse(
                UserId: user.UserId,
                FullName: user.FullName,
                Email: user.Email,
                Role: user.Role,
                Token: $"demo-token-{user.UserId}-{Guid.NewGuid():N}",
                Message: "Xác thực email thành công! Tài khoản đã được kích hoạt.");

            return Result<AuthResponse>.SuccessResult(response);
        }, cancellationToken);
    }

    public async Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(resendOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            PendingRegistration? pending = await session.Auth.FindPendingRegistrationAsync(request.Email, cancellationToken);
            if (pending == null)
            {
                return Result<string>.FailureResult(new Error(
                    "NOT_FOUND", "Không tìm thấy phiên đăng ký cho email này. Vui lòng đăng ký lại.", "NotFound"));
            }

            string newOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var renewed = pending with
            {
                OtpCode = newOtp,
                OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                AttemptCount = 0
            };

            await session.Auth.UpdatePendingRegistrationAsync(renewed, cancellationToken);

            return Result<string>.SuccessResult($"Mã OTP mới đã được gửi lại tới {request.Email}. [Demo OTP: {newOtp}]");
        }, cancellationToken);
    }
}
