using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class ProfileRepository(AgriSustainContext db) : IProfileRepository
{
    public async Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
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
            defaultAddress.DistrictCode,
            defaultAddress.WardCode,
            defaultAddress.IsDefault);

        FarmerProfileDto? farmerDto = user.FarmerProfile == null ? null : new FarmerProfileDto(
            user.FarmerProfile.FarmerProfileId,
            user.FarmerProfile.FarmName,
            user.FarmerProfile.RepresentativeName,
            user.FarmerProfile.ProductionScale,
            user.FarmerProfile.CultivatedArea,
            user.FarmerProfile.CultivationZone,
            user.FarmerProfile.Gpscoordinates,
            user.FarmerProfile.BusinessLicenseNumber,
            user.FarmerProfile.Address,
            user.FarmerProfile.ProvinceCode,
            user.FarmerProfile.VerificationStatus,
            user.FarmerProfile.ReputationScore,
            user.FarmerProfile.ApprovedAt,
            user.FarmerProfile.RejectReason,
            user.FarmerProfile.ProductionModel,
            user.FarmerProfile.ShortDescription);

        long rewardPoints = await db.RewardPointEntries
            .Where(entry => entry.UserId == userId)
            .SumAsync(entry => entry.AvailableDelta ?? 0, cancellationToken);

        return new UserProfileResponse(
            UserId: user.UserId,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            Email: user.Email,
            Role: user.Role,
            Status: user.Status,
            AvatarUrl: user.AvatarUrl,
            Gender: user.Gender,
            BirthDate: user.DateOfBirth,
            RewardPoints: rewardPoints,
            DeliveryAddress: addressDto,
            FarmerProfile: farmerDto);
    }

    public async Task<ProfileAccount?> GetAccountAsync(long userId, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new ProfileAccount(u.UserId, u.Role, u.Status))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> PhoneUsedByAnotherUserAsync(string phone, long userId, CancellationToken cancellationToken) =>
        await db.Users.AnyAsync(u => u.PhoneNumber == phone && u.UserId != userId, cancellationToken);

    public async Task<UserProfileResponse?> UpdateConsumerProfileAsync(
        long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.DeliveryAddress)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return null;

        var before = new
        {
            user.FullName, user.PhoneNumber,
            Address = user.DeliveryAddress == null ? null : new
            {
                user.DeliveryAddress.ProvinceCode, user.DeliveryAddress.DistrictCode,
                user.DeliveryAddress.WardCode, user.DeliveryAddress.AddressDetail
            }
        };

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.Gender = NormalizeGender(request.Gender);
        user.DateOfBirth = request.BirthDate;

        // The service has already normalized the address against the checkout catalog.
        if (!string.IsNullOrWhiteSpace(request.ProvinceCode)
            && request.ProvinceCity is { } province
            && request.Ward is { } ward
            && request.AddressDetail is { } detail)
        {
            var address = user.DeliveryAddress;
            if (address == null)
            {
                address = new DeliveryAddress
                {
                    UserId = user.UserId,
                    IsDefault = true,
                    Status = "Active"
                };
                db.DeliveryAddresses.Add(address);
            }

            address.RecipientName = string.IsNullOrWhiteSpace(request.RecipientName)
                ? user.FullName : request.RecipientName.Trim();
            address.RecipientPhone = string.IsNullOrWhiteSpace(request.RecipientPhone)
                ? user.PhoneNumber : request.RecipientPhone.Trim();
            address.ProvinceCity = province;
            address.District = request.District;
            address.Ward = ward;
            address.AddressDetail = detail.Trim();
            address.ProvinceCode = request.ProvinceCode;
            address.DistrictCode = request.DistrictCode;
            address.WardCode = request.WardCode;
            address.AddressCatalogVersion = request.CatalogVersion;
        }

        AddAudit(userId, "User", userId, "UpdateProfile", before, new
        {
            user.FullName, user.PhoneNumber, user.Gender, user.DateOfBirth,
            Address = new { request.ProvinceCode, request.DistrictCode, request.WardCode, request.AddressDetail }
        });

        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfileResponse?> UpdateFarmerProfileAsync(
        long userId, UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user?.FarmerProfile == null) return null;
        var farm = user.FarmerProfile;

        var before = FarmSnapshot(user, farm);

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();

        bool kycFieldChanged =
            !SameText(farm.BusinessLicenseNumber, request.BusinessLicenseNumber)
            || !SameText(farm.RepresentativeName, request.RepresentativeName);

        farm.FarmName = request.FarmName.Trim();
        farm.RepresentativeName = request.RepresentativeName.Trim();
        farm.Address = request.Address.Trim();
        if (!string.IsNullOrWhiteSpace(request.ProvinceCode)) farm.ProvinceCode = request.ProvinceCode;
        farm.ProductionScale = Clean(request.ProductionScale);
        farm.CultivationZone = Clean(request.CultivationZone);
        farm.Gpscoordinates = Clean(request.GPSCoordinates);
        farm.BusinessLicenseNumber = Clean(request.BusinessLicenseNumber);
        farm.CultivatedArea = request.CultivatedArea;
        farm.ProductionModel = Clean(request.ProductionModel);
        farm.ShortDescription = Clean(request.ShortDescription);

        // SRS UC-AUTH-03 AF "Changing Verified Identity / KYC Fields".
        if (kycFieldChanged && farm.VerificationStatus == VerificationStatuses.Approved)
        {
            farm.VerificationStatus = VerificationStatuses.PendingApproval;
            farm.ApprovedAt = null;
            farm.RejectReason = null;
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Hồ sơ nông trại cần xác minh lại",
                Content = "Bạn vừa thay đổi thông tin pháp lý (giấy phép / người đại diện). "
                    + "Hồ sơ đã được chuyển cho Nhân viên xác minh duyệt lại.",
                NotificationType = "KycReverification",
                IsRead = false,
                SentAt = DateTime.UtcNow,
                DeliveryChannel = "InApp",
                DeliveryStatus = "Sent",
                RetryCount = 0,
                RelatedRecordType = "FarmerProfile",
                RelatedRecordId = farm.FarmerProfileId
            });
        }

        AddAudit(userId, "FarmerProfile", farm.FarmerProfileId, "UpdateProfile", before, FarmSnapshot(user, farm));
        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<KycSubmissionResult> SubmitFarmerKycAsync(
        long userId, SubmitKycRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        var farm = user?.FarmerProfile;
        if (user == null || farm == null)
        {
            return new KycSubmissionResult(KycSubmissionOutcome.NotFound, [], null, null);
        }

        if (!VerificationStatuses.CanSubmit(farm.VerificationStatus))
        {
            return new KycSubmissionResult(KycSubmissionOutcome.InvalidState, [], farm.VerificationStatus, null);
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(farm.FarmName)) missing.Add("farmName");
        if (string.IsNullOrWhiteSpace(farm.RepresentativeName)) missing.Add("representativeName");
        if (string.IsNullOrWhiteSpace(user.PhoneNumber)) missing.Add("phoneNumber");
        if (string.IsNullOrWhiteSpace(farm.Address)) missing.Add("address");
        if (string.IsNullOrWhiteSpace(farm.CultivationZone)) missing.Add("cultivationZone");
        if (string.IsNullOrWhiteSpace(farm.BusinessLicenseNumber)) missing.Add("businessLicenseNumber");
        // BR-BA-03: a payout bank account is required before submitting for verification.
        if (!await db.UserBankAccounts.AnyAsync(a => a.UserId == userId && a.IsDefault, cancellationToken))
            missing.Add("bankAccount");
        if (missing.Count > 0)
        {
            return new KycSubmissionResult(KycSubmissionOutcome.Incomplete, missing, farm.VerificationStatus, null);
        }

        string oldStatus = farm.VerificationStatus;
        farm.VerificationStatus = VerificationStatuses.PendingApproval;
        farm.RejectReason = null;
        AddAudit(userId, "FarmerProfile", farm.FarmerProfileId, "SubmitForVerification",
            new { VerificationStatus = oldStatus },
            new { farm.VerificationStatus, Notes = request.Notes });

        await db.SaveChangesAsync(cancellationToken);
        return new KycSubmissionResult(KycSubmissionOutcome.Submitted, [], farm.VerificationStatus,
            await GetProfileAsync(userId, cancellationToken));
    }

    public async Task<UserProfileResponse?> UpdateAvatarAsync(
        long userId, string avatarUrl, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null) return null;

        AddAudit(userId, "User", userId, "UpdateAvatar", new { user.AvatarUrl }, new { AvatarUrl = avatarUrl });
        user.AvatarUrl = avatarUrl;
        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    /// <summary>Accepts the codes and the Vietnamese labels used by older UI builds.</summary>
    private static string? NormalizeGender(string? value) => value?.Trim() switch
    {
        "Male" or "Nam" => "Male",
        "Female" or "Nữ" => "Female",
        "Other" or "Khác" => "Other",
        _ => null
    };

    private void AddAudit(long userId, string entityType, long entityId, string action, object before, object after) =>
        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(after),
            OccurredAt = DateTime.UtcNow
        });

    private static object FarmSnapshot(User user, FarmerProfile farm) => new
    {
        user.FullName, user.PhoneNumber, farm.FarmName, farm.RepresentativeName, farm.Address,
        farm.ProvinceCode, farm.ProductionScale, farm.CultivationZone, farm.Gpscoordinates,
        farm.BusinessLicenseNumber, farm.CultivatedArea, farm.ProductionModel, farm.ShortDescription,
        farm.VerificationStatus
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool SameText(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
}
