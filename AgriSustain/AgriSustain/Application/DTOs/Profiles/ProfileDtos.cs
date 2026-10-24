namespace AgriSustain.Application.DTOs.Profiles;

public sealed record UserProfileResponse(
    long UserId,
    string FullName,
    string? PhoneNumber,
    string Email,
    string Role,
    string Status,
    string? AvatarUrl,
    string? Gender,
    DateOnly? BirthDate,
    long RewardPoints,
    DeliveryAddressDto? DeliveryAddress,
    FarmerProfileDto? FarmerProfile);

public sealed record DeliveryAddressDto(
    long DeliveryAddressId,
    string RecipientName,
    string RecipientPhone,
    string ProvinceCity,
    string? District,
    string Ward,
    string AddressDetail,
    string? ProvinceCode,
    string? WardCode,
    bool IsDefault);

public sealed record FarmerProfileDto(
    long FarmerProfileId,
    string FarmName,
    string RepresentativeName,
    string? ProductionScale,
    decimal? CultivatedArea,
    string? CultivationZone,
    string? GPSCoordinates,
    string? BusinessLicenseNumber,
    string Address,
    string ProvinceCode,
    string VerificationStatus,
    decimal ReputationScore,
    DateTimeOffset? ApprovedAt,
    string? RejectReason);

public sealed record UpdateConsumerProfileRequest(
    string FullName,
    string PhoneNumber,
    string? Gender,
    DateOnly? BirthDate,
    string? ProvinceCity,
    string? District,
    string? Ward,
    string? AddressDetail,
    string? ProvinceCode,
    string? WardCode);

public sealed record UpdateFarmerProfileRequest(
    string FullName,
    string PhoneNumber,
    string FarmName,
    string RepresentativeName,
    string Address,
    string? ProvinceCode,
    string? CultivationZone,
    string? ProductionScale,
    decimal? CultivatedArea,
    string? GPSCoordinates,
    string? BusinessLicenseNumber);

public sealed record SubmitKycRequest(
    string? Notes);
