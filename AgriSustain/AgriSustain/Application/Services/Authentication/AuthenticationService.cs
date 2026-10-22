using System.Security.Cryptography;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Application.Services.Authentication;

public sealed class AuthenticationService(
    AgriSustainContext context,
    SqlDemoDependencies demoStore,
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

        if (await context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
        {
            return Result<string>.FailureResult(new Error(
                "EMAIL_EXISTS", "Email này đã được sử dụng bởi một tài khoản khác.", "Conflict"));
        }

        if (await context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber, cancellationToken))
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

        demoStore.PendingRegistrations[request.Email] = pending;

        return Result<string>.SuccessResult($"Mã xác thực OTP đã được gửi đến {request.Email}. Mã có hiệu lực trong 5 phút. [Demo OTP: {otp}]");
    }

    public async Task<Result<AuthResponse>> VerifyOtpAndCompleteRegistrationAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(verifyOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<AuthResponse>.FailureResult(error);
        }

        demoStore.PendingRegistrations.TryGetValue(request.Email, out var pending);
        if (pending == null)
        {
            return Result<AuthResponse>.FailureResult(new Error(
                "REGISTRATION_EXPIRED", "Yêu cầu đăng ký không tồn tại hoặc đã hết hạn. Vui lòng đăng ký lại.", "NotFound"));
        }

        if (DateTimeOffset.UtcNow > pending.OtpExpiresAt)
        {
            demoStore.PendingRegistrations.TryRemove(request.Email, out _);
            return Result<AuthResponse>.FailureResult(new Error(
                "OTP_EXPIRED", "Mã OTP đã hết hiệu lực. Vui lòng yêu cầu gửi lại mã mới.", "Validation"));
        }

        if (pending.OtpCode != request.OtpCode)
        {
            if (pending.AttemptCount >= 4)
            {
                demoStore.PendingRegistrations.TryRemove(request.Email, out _);
                return Result<AuthResponse>.FailureResult(new Error(
                    "MAX_ATTEMPTS_EXCEEDED", "Bạn đã nhập sai mã OTP quá 5 lần. Yêu cầu đăng ký đã bị hủy vì lý do bảo mật.", "Validation"));
            }

            demoStore.PendingRegistrations[request.Email] = pending with { AttemptCount = pending.AttemptCount + 1 };
            return Result<AuthResponse>.FailureResult(new Error(
                "INVALID_OTP", $"Mã OTP không chính xác. Bạn còn {4 - pending.AttemptCount} lần thử.", "Validation"));
        }

        var user = new User
        {
            Username = pending.Email.Split('@')[0] + "_" + Random.Shared.Next(100, 999),
            Email = pending.Email,
            PasswordHash = pending.PasswordHash,
            FullName = pending.FullName,
            PhoneNumber = pending.PhoneNumber,
            Role = pending.Role,
            Status = "Active",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            FailedLoginCount = 0
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        if (pending.Role == "Farmer")
        {
            var farmerProfile = new FarmerProfile
            {
                UserId = user.UserId,
                FarmName = "Nông trại " + pending.FullName,
                RepresentativeName = pending.FullName,
                Address = "Chưa cập nhật địa chỉ",
                ProvinceCode = "01",
                VerificationStatus = "Pending",
                ReputationScore = 5.0m
            };
            context.FarmerProfiles.Add(farmerProfile);
            await context.SaveChangesAsync(cancellationToken);
        }

        demoStore.PendingRegistrations.TryRemove(request.Email, out _);

        var response = new AuthResponse(
            UserId: user.UserId,
            FullName: user.FullName,
            Email: user.Email,
            Role: user.Role,
            Token: $"demo-token-{user.UserId}-{Guid.NewGuid():N}",
            Message: "Xác thực email thành công! Tài khoản đã được kích hoạt.");

        return Result<AuthResponse>.SuccessResult(response);
    }

    public async Task<Result<string>> ResendOtpAsync(
        ResendOtpRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(resendOtpValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<string>.FailureResult(error);
        }

        demoStore.PendingRegistrations.TryGetValue(request.Email, out var pending);
        if (pending == null)
        {
            return Result<string>.FailureResult(new Error(
                "NOT_FOUND", "Không tìm thấy phiên đăng ký cho email này. Vui lòng đăng ký lại.", "NotFound"));
        }

        string newOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        demoStore.PendingRegistrations[request.Email] = pending with
        {
            OtpCode = newOtp,
            OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            AttemptCount = 0
        };

        return Result<string>.SuccessResult($"Mã OTP mới đã được gửi lại tới {request.Email}. [Demo OTP: {newOtp}]");
    }
}
