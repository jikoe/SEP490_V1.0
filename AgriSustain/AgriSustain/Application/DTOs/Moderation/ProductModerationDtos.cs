namespace AgriSustain.Application.DTOs.Moderation;

public sealed record ProductModerationListItemDto(
    long Id,
    string Name,
    string Status,
    string Category,
    string FarmerName,
    string FarmName,
    DateTime SubmittedAt,
    string? ImageUrl);

public sealed record ProductModerationListDto(
    IReadOnlyList<ProductModerationListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ProductModerationDetailDto(
    long Id,
    string ModerationStatus,
    string? RejectionReason,
    string Name,
    string? Description,
    string Category,
    IReadOnlyList<ProductModerationImageDto> Images,
    decimal RetailPrice,
    decimal? WholesalePrice,
    decimal? WholesaleMinQuantity,
    string Unit,
    decimal Quantity,
    ProductModerationFarmerDto Farmer,
    ProductModerationFarmDto Farm,
    IReadOnlyList<ProductModerationHarvestDto> Harvest,
    IReadOnlyList<ProductModerationTraceabilityDto> Traceability,
    IReadOnlyList<ProductModerationCertificateDto> Certificates);

public sealed record ProductModerationImageDto(string ImageUrl, string? AltText);

public sealed record ProductModerationFarmerDto(
    string FullName,
    string Email,
    string? PhoneNumber);

public sealed record ProductModerationFarmDto(
    string FarmName,
    string? Address,
    string? ProductionScale,
    string? ProvinceCode,
    string VerificationStatus);

public sealed record ProductModerationHarvestDto(
    string BatchCode,
    decimal HarvestYield,
    decimal AvailableQuantity,
    DateOnly? EstimatedHarvestDate,
    DateOnly? ActualHarvestDate,
    DateOnly? ExpirationDate,
    string? QualityNotes,
    string Season,
    string CropVariety);

public sealed record ProductModerationTraceabilityDto(
    string BatchCode,
    string? TargetUrl,
    string? QrcontentString,
    bool IsRevoked);

public sealed record ProductModerationCertificateDto(
    long FarmCertificateId,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly? ExpiryDate,
    string DocumentUrl,
    string VerificationStatus);

public sealed record RejectProductRequest(string? Reason);

public enum ProductModerationOutcome
{
    Success,
    NotFound,
    Conflict,
    InvalidReason
}
