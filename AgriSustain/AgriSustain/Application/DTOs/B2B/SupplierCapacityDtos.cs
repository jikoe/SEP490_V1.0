namespace AgriSustain.Application.DTOs.B2B;

public sealed record SupplierCapacityDirectoryItemDto(
    long FarmerProfileId,
    long UserId,
    string FarmName,
    string RepresentativeName,
    string? AvatarUrl,
    string ProvinceCode,
    string ProvinceName,
    string Address,
    string? CultivationZone,
    string? ProductionModel,
    decimal? CultivatedArea,
    string? ProductionScale,
    string VerificationStatus,
    bool IsVerified,
    decimal ReputationScore,
    int ActiveSeasonCount,
    int AvailableProductCount,
    IReadOnlyList<string> CertifiedBadges);

public sealed record SupplierCapacityProfileDto(
    long FarmerProfileId,
    long UserId,
    string FarmName,
    string RepresentativeName,
    string LegalName,
    string? AvatarUrl,
    string Email,
    string? PhoneNumber,
    string ProvinceCode,
    string ProvinceName,
    string Address,
    string? CultivationZone,
    string? GpsCoordinates,
    string? ProductionModel,
    decimal? CultivatedArea,
    string? ProductionScale,
    string? ShortDescription,
    string? StorageInfrastructure,
    IReadOnlyList<string> FarmingMethods,
    string VerificationStatus,
    bool IsVerified,
    DateTime? ApprovedAt,
    decimal ReputationScore,
    // Production Capacity & Timeline
    IReadOnlyList<SupplierCropSeasonDto> CropCycles,
    IReadOnlyList<SupplierInventoryItemDto> AvailableInventory,
    // Credentials & Quality Assurance
    IReadOnlyList<SupplierCertificateDto> Certificates,
    IReadOnlyList<SupplierTraceabilityBatchDto> TraceabilityBatches,
    // Historical Performance & Reviews
    SupplierPerformanceStatsDto PerformanceStats,
    IReadOnlyList<SupplierReviewDto> RecentReviews);

public sealed record SupplierCropSeasonDto(
    long CropSeasonId,
    string SeasonName,
    string CropVariety,
    long ProductId,
    string ProductName,
    decimal CultivatedArea,
    DateOnly PlantingDate,
    DateOnly EstimatedHarvestDate,
    DateOnly? ActualHarvestDate,
    decimal EstimatedYield,
    decimal? ActualYield,
    string YieldUnit,
    string Status,
    IReadOnlyList<SupplierCultivationLogDto> CultivationLogs);

public sealed record SupplierCultivationLogDto(
    long CultivationLogId,
    DateTime ActionDate,
    string ActionType,
    string? DetailDescription,
    string? InputMaterial,
    string? Dosage,
    string? PerformedBy,
    string? ImageUrl);

public sealed record SupplierInventoryItemDto(
    long ProductId,
    string ProductName,
    string Unit,
    decimal BaseRetailPrice,
    decimal? WholesalePrice,
    decimal? WholesaleMinQuantity,
    decimal AvailableQuantity,
    string? OriginProvince,
    string? PrimaryImageUrl);

public sealed record SupplierCertificateDto(
    long FarmCertificateId,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string DocumentUrl,
    string VerificationStatus,
    bool IsValid);

public sealed record SupplierCertificateDossierDto(
    long FarmCertificateId,
    long FarmerProfileId,
    string FarmName,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string DocumentUrl,
    string VerificationStatus,
    bool IsValid,
    DateTime? ReviewedAt,
    string? ReviewedByOfficerName,
    string? RevocationReason);

public sealed record SupplierTraceabilityBatchDto(
    long HarvestBatchId,
    string BatchCode,
    long ProductId,
    string ProductName,
    DateOnly? ActualHarvestDate,
    decimal AvailableQuantity,
    string Unit,
    string? QrContentString,
    string? TargetUrl,
    int ScanCount,
    int CultivationLogCount);

public sealed record SupplierPerformanceStatsDto(
    int TotalOrders,
    int CompletedOrders,
    double FulfillmentRate,
    int DisputeCount,
    double DisputeRate,
    double AverageRating,
    int ReviewCount);

public sealed record SupplierReviewDto(
    long ReviewId,
    string BuyerName,
    int RatingStars,
    string? Comment,
    DateTime ReviewDate,
    string ProductName);

