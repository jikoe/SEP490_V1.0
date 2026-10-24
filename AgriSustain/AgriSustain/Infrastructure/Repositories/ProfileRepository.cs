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

        long rewardPoints = await db.RewardPointEntries
            .Where(entry => entry.UserId == userId)
            .SumAsync(entry => entry.AvailableDelta ?? 0, cancellationToken);

        // The User table has no gender/birth-date columns, so these are not reported.
        return new UserProfileResponse(
            UserId: user.UserId,
            FullName: user.FullName,
            PhoneNumber: user.PhoneNumber,
            Email: user.Email,
            Role: user.Role,
            Status: user.Status,
            AvatarUrl: user.AvatarUrl,
            Gender: null,
            BirthDate: null,
            RewardPoints: rewardPoints,
            DeliveryAddress: addressDto,
            FarmerProfile: farmerDto);
    }

    public async Task<UserProfileResponse?> UpdateConsumerProfileAsync(
        long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.DeliveryAddress)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return null;

        user.FullName = request.FullName;
        user.PhoneNumber = request.PhoneNumber;

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

            address.RecipientName = request.FullName;
            address.RecipientPhone = request.PhoneNumber;
            address.ProvinceCity = province;
            address.District = null;
            address.Ward = ward;
            address.AddressDetail = detail;
            address.ProvinceCode = request.ProvinceCode;
            address.WardCode = request.WardCode;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfileResponse?> UpdateFarmerProfileAsync(
        long userId, UpdateFarmerProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.FarmerProfile)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) return null;

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
            db.FarmerProfiles.Add(user.FarmerProfile);
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

        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfileResponse?> SubmitFarmerKycAsync(
        long userId, SubmitKycRequest request, CancellationToken cancellationToken)
    {
        var farmer = await db.FarmerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);
        if (farmer == null) return null;

        farmer.VerificationStatus = "PendingVerification";
        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(userId, cancellationToken);
    }
}
