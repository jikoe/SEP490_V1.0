using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Application.Services.Profiles;

public sealed class ProfileService(
    AgriSustainContext context,
    ICurrentUser currentUser,
    IValidator<UpdateConsumerProfileRequest> consumerValidator,
    IValidator<UpdateFarmerProfileRequest> farmerValidator) : IProfileService
{
    private async Task<UserProfileResponse?> MapProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.DeliveryAddress)
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return null;

        var defaultAddress = user.DeliveryAddress;
        DeliveryAddressDto? addressDto = defaultAddress == null ? null : new DeliveryAddressDto(
            defaultAddress.DeliveryAddressId,
            defaultAddress.RecipientName,
            defaultAddress.RecipientPhone,
            defaultAddress.ProvinceCity,
            defaultAddress.District,
            defaultAddress.Ward,
            defaultAddress.AddressDetail,
            defaultAddress.ProvinceCode,
            defaultAddress.WardCode,
            defaultAddress.IsDefault);

        FarmerProfileDto? farmerDto = user.FarmerProfile == null ? null : new FarmerProfileDto(
            user.FarmerProfile.FarmerProfileId,
            user.FarmerProfile.FarmName,
            user.FarmerProfile.RepresentativeName,
            user.FarmerProfile.ProductionScale,
            null,
            user.FarmerProfile.CultivationZone,
            user.FarmerProfile.Gpscoordinates,
            user.FarmerProfile.BusinessLicenseNumber,
            user.FarmerProfile.Address,
            user.FarmerProfile.ProvinceCode,
            user.FarmerProfile.VerificationStatus,
            user.FarmerProfile.ReputationScore,
            user.FarmerProfile.ApprovedAt,
            user.FarmerProfile.RejectReason);

        return new UserProfileResponse(
            UserId: user.UserId,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            Email: user.Email,
            Role: user.Role,
            Status: user.Status,
            AvatarUrl: user.AvatarUrl,
            Gender: "Nam",
            BirthDate: new DateOnly(1990, 1, 1),
            RewardPoints: 120,
            DeliveryAddress: addressDto,
            FarmerProfile: farmerDto);
    }

    public async Task<Result<UserProfileResponse>> GetCurrentProfileAsync(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "UNAUTHORIZED", "Vui lòng đăng nhập để xem thông tin hồ sơ.", "Unauthorized"));
        }

        var profile = await MapProfileAsync(currentUser.UserId.Value, cancellationToken);
        return profile == null
            ? Result<UserProfileResponse>.FailureResult(new Error(
                "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound"))
            : Result<UserProfileResponse>.SuccessResult(profile);
    }

    public async Task<Result<UserProfileResponse>> GetProfileByIdAsync(
        long userId, CancellationToken cancellationToken)
    {
        var profile = await MapProfileAsync(userId, cancellationToken);
        return profile == null
            ? Result<UserProfileResponse>.FailureResult(new Error(
                "USER_NOT_FOUND", $"Không tìm thấy người dùng có mã ID #{userId}.", "NotFound"))
            : Result<UserProfileResponse>.SuccessResult(profile);
    }

    public async Task<Result<UserProfileResponse>> UpdateConsumerProfileAsync(
        UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(consumerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        if (currentUser.UserId == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "UNAUTHORIZED", "Vui lòng đăng nhập trước khi cập nhật hồ sơ.", "Unauthorized"));
        }

        long userId = currentUser.UserId.Value;
        var user = await context.Users
            .Include(u => u.DeliveryAddress)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "USER_NOT_FOUND", "Không tìm thấy người dùng.", "NotFound"));
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(request.AddressDetail) || !string.IsNullOrWhiteSpace(request.ProvinceCity))
        {
            var address = user.DeliveryAddress;
            if (address == null)
            {
                address = new DeliveryAddress
                {
                    UserId = user.UserId,
                    RecipientName = request.FullName,
                    RecipientPhone = request.PhoneNumber,
                    ProvinceCity = request.ProvinceCity ?? "Hà Nội",
                    District = request.District,
                    Ward = request.Ward ?? "Dịch Vọng Hậu",
                    AddressDetail = request.AddressDetail ?? "Chưa rõ",
                    ProvinceCode = request.ProvinceCode,
                    WardCode = request.WardCode,
                    IsDefault = true,
                    Status = "Active"
                };
                context.DeliveryAddresses.Add(address);
            }
            else
            {
                address.RecipientName = request.FullName;
                address.RecipientPhone = request.PhoneNumber;
                if (!string.IsNullOrWhiteSpace(request.ProvinceCity)) address.ProvinceCity = request.ProvinceCity;
                if (!string.IsNullOrWhiteSpace(request.District)) address.District = request.District;
                if (!string.IsNullOrWhiteSpace(request.Ward)) address.Ward = request.Ward;
                if (!string.IsNullOrWhiteSpace(request.AddressDetail)) address.AddressDetail = request.AddressDetail;
                if (!string.IsNullOrWhiteSpace(request.ProvinceCode)) address.ProvinceCode = request.ProvinceCode;
                if (!string.IsNullOrWhiteSpace(request.WardCode)) address.WardCode = request.WardCode;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        var updated = await MapProfileAsync(userId, cancellationToken);
        return Result<UserProfileResponse>.SuccessResult(updated!);
    }

    public async Task<Result<UserProfileResponse>> UpdateFarmerProfileAsync(
        UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(farmerValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserProfileResponse>.FailureResult(error);
        }

        if (currentUser.UserId == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "UNAUTHORIZED", "Vui lòng đăng nhập trước khi cập nhật hồ sơ.", "Unauthorized"));
        }

        long userId = currentUser.UserId.Value;
        var user = await context.Users
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "USER_NOT_FOUND", "Không tìm thấy người dùng.", "NotFound"));
        }

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;

        if (user.FarmerProfile == null)
        {
            user.FarmerProfile = new FarmerProfile
            {
                UserId = user.UserId,
                FarmName = request.FarmName,
                RepresentativeName = request.RepresentativeName,
                Address = request.Address,
                ProvinceCode = request.ProvinceCode ?? "01",
                ProductionScale = request.ProductionScale,
                CultivationZone = request.CultivationZone,
                Gpscoordinates = request.GPSCoordinates,
                BusinessLicenseNumber = request.BusinessLicenseNumber,
                VerificationStatus = "Pending",
                ReputationScore = 5.0m
            };
            context.FarmerProfiles.Add(user.FarmerProfile);
        }
        else
        {
            user.FarmerProfile.FarmName = request.FarmName;
            user.FarmerProfile.RepresentativeName = request.RepresentativeName;
            user.FarmerProfile.Address = request.Address;
            if (!string.IsNullOrWhiteSpace(request.ProvinceCode)) user.FarmerProfile.ProvinceCode = request.ProvinceCode;
            user.FarmerProfile.ProductionScale = request.ProductionScale;
            user.FarmerProfile.CultivationZone = request.CultivationZone;
            user.FarmerProfile.Gpscoordinates = request.GPSCoordinates;
            user.FarmerProfile.BusinessLicenseNumber = request.BusinessLicenseNumber;
        }

        await context.SaveChangesAsync(cancellationToken);
        var updated = await MapProfileAsync(userId, cancellationToken);
        return Result<UserProfileResponse>.SuccessResult(updated!);
    }

    public async Task<Result<UserProfileResponse>> SubmitFarmerKycAsync(
        SubmitKycRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "UNAUTHORIZED", "Vui lòng đăng nhập trước khi nộp hồ sơ KYC.", "Unauthorized"));
        }

        long userId = currentUser.UserId.Value;
        var farmer = await context.FarmerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);
        if (farmer == null)
        {
            return Result<UserProfileResponse>.FailureResult(new Error(
                "KYC_FAILED", "Không thể nộp hồ sơ xác thực nông hộ.", "NotFound"));
        }

        farmer.VerificationStatus = "PendingVerification";
        await context.SaveChangesAsync(cancellationToken);

        var updated = await MapProfileAsync(userId, cancellationToken);
        return Result<UserProfileResponse>.SuccessResult(updated!);
    }
}
