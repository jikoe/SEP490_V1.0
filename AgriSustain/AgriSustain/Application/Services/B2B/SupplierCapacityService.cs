using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.B2B;

public class SupplierCapacityService(ISupplierCapacityRepository repository) : ISupplierCapacityService
{
    private readonly ISupplierCapacityRepository _repository = repository;

    public async Task<Result<SupplierCapacityProfileDto>> GetCapacityProfileAsync(
        long farmerProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _repository.GetFarmerProfileWithDetailsAsync(farmerProfileId, cancellationToken);
        if (profile == null)
        {
            return Result<SupplierCapacityProfileDto>.Fail("Không tìm thấy thông tin hồ sơ nhà cung cấp", 404);
        }

        var dto = await BuildCapacityProfileDtoAsync(profile, cancellationToken);
        return Result<SupplierCapacityProfileDto>.Ok(dto);
    }

    public async Task<Result<SupplierCapacityProfileDto>> GetCapacityProfileByUserIdAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var profile = await _repository.GetFarmerProfileByUserIdAsync(userId, cancellationToken);
        if (profile == null)
        {
            return Result<SupplierCapacityProfileDto>.Fail("Không tìm thấy thông tin hồ sơ nhà cung cấp từ tài khoản người dùng", 404);
        }

        var dto = await BuildCapacityProfileDtoAsync(profile, cancellationToken);
        return Result<SupplierCapacityProfileDto>.Ok(dto);
    }

    public async Task<Result<IReadOnlyList<SupplierCapacityDirectoryItemDto>>> GetDirectoryAsync(
        string? keyword,
        string? province,
        CancellationToken cancellationToken)
    {
        var suppliers = await _repository.GetSuppliersDirectoryAsync(keyword, province, cancellationToken);

        var list = suppliers.Select(s =>
        {
            var isVerified = string.Equals(s.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(s.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase);

            var badges = s.FarmCertificates
                .Where(c => (string.Equals(c.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(c.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase)) &&
                            (!c.ExpiryDate.HasValue || c.ExpiryDate.Value >= DateOnly.FromDateTime(DateTime.UtcNow)))
                .Select(c => c.CertificateType)
                .Distinct()
                .ToList();

            var activeSeasonCount = s.CropSeasons.Count(cs =>
                !string.Equals(cs.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(cs.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            var availableProductCount = s.Products.Count(p =>
                string.Equals(p.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) &&
                p.IsPublished);

            return new SupplierCapacityDirectoryItemDto(
                FarmerProfileId: s.FarmerProfileId,
                UserId: s.UserId,
                FarmName: s.FarmName,
                RepresentativeName: s.RepresentativeName,
                AvatarUrl: s.User?.AvatarUrl,
                ProvinceCode: s.ProvinceCode,
                ProvinceName: ResolveProvinceName(s.ProvinceCode, s.Address),
                Address: s.Address,
                CultivationZone: s.CultivationZone,
                ProductionModel: s.ProductionModel,
                CultivatedArea: s.CultivatedArea,
                ProductionScale: s.ProductionScale,
                VerificationStatus: s.VerificationStatus,
                IsVerified: isVerified,
                ReputationScore: s.ReputationScore,
                ActiveSeasonCount: activeSeasonCount,
                AvailableProductCount: availableProductCount,
                CertifiedBadges: badges);
        }).ToList();

        return Result<IReadOnlyList<SupplierCapacityDirectoryItemDto>>.Ok(list);
    }

    public async Task<Result<SupplierCertificateDossierDto>> GetCertificateDossierAsync(
        long certificateId,
        CancellationToken cancellationToken)
    {
        var cert = await _repository.GetCertificateDossierAsync(certificateId, cancellationToken);
        if (cert == null)
        {
            return Result<SupplierCertificateDossierDto>.Fail("Không tìm thấy hồ sơ thẩm định chứng chỉ chất lượng", 404);
        }

        var isVerified = string.Equals(cert.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cert.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase);

        var isExpired = cert.ExpiryDate.HasValue && cert.ExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
        var isValid = isVerified && !isExpired;

        var dossier = new SupplierCertificateDossierDto(
            FarmCertificateId: cert.FarmCertificateId,
            FarmerProfileId: cert.FarmerProfileId,
            FarmName: cert.FarmerProfile?.FarmName ?? string.Empty,
            CertificateType: cert.CertificateType,
            CertificateNumber: cert.CertificateNumber,
            IssuedBy: cert.IssuedBy,
            IssueDate: cert.IssueDate,
            ExpiryDate: cert.ExpiryDate,
            DocumentUrl: cert.DocumentUrl,
            VerificationStatus: cert.VerificationStatus,
            IsValid: isValid,
            ReviewedAt: cert.ReviewedAt,
            ReviewedByOfficerName: cert.ReviewedByUser?.FullName ?? "Chuyên viên Thẩm định Hệ thống",
            RevocationReason: cert.RevocationReason);

        return Result<SupplierCertificateDossierDto>.Ok(dossier);
    }

    public async Task<Result<IReadOnlyList<SupplierCultivationLogDto>>> GetSeasonCultivationLogsAsync(
        long cropSeasonId,
        CancellationToken cancellationToken)
    {
        var logs = await _repository.GetSeasonCultivationLogsAsync(cropSeasonId, cancellationToken);

        var dtoList = logs.Select(l => new SupplierCultivationLogDto(
            CultivationLogId: l.CultivationLogId,
            ActionDate: l.ActionDate,
            ActionType: l.ActionType,
            DetailDescription: l.DetailDescription,
            InputMaterial: l.InputMaterial,
            Dosage: l.Dosage,
            PerformedBy: l.PerformedBy ?? l.RecordedByUser?.FullName,
            ImageUrl: l.ImageUrl
        )).ToList();

        return Result<IReadOnlyList<SupplierCultivationLogDto>>.Ok(dtoList);
    }

    private async Task<SupplierCapacityProfileDto> BuildCapacityProfileDtoAsync(
        FarmerProfile profile,
        CancellationToken cancellationToken)
    {
        var isVerified = string.Equals(profile.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(profile.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase);

        var performanceStats = await _repository.GetFarmerPerformanceStatsAsync(profile.FarmerProfileId, cancellationToken);
        var recentReviews = await _repository.GetRecentReviewsAsync(profile.FarmerProfileId, 10, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // AF5: Certificates (empty list if none)
        var certificates = (profile.FarmCertificates ?? [])
            .Select(c =>
            {
                var isCertVerified = string.Equals(c.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(c.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase);
                var isCertValid = isCertVerified && (!c.ExpiryDate.HasValue || c.ExpiryDate.Value >= today);

                return new SupplierCertificateDto(
                    FarmCertificateId: c.FarmCertificateId,
                    CertificateType: c.CertificateType,
                    CertificateNumber: c.CertificateNumber,
                    IssuedBy: c.IssuedBy,
                    IssueDate: c.IssueDate,
                    ExpiryDate: c.ExpiryDate,
                    DocumentUrl: c.DocumentUrl,
                    VerificationStatus: c.VerificationStatus,
                    IsValid: isCertValid);
            })
            .OrderByDescending(c => c.IsValid)
            .ThenBy(c => c.CertificateType)
            .ToList();

        // Farming methods synthesis
        var farmingMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cert in certificates.Where(c => c.IsValid))
        {
            farmingMethods.Add(cert.CertificateType);
        }
        if (!string.IsNullOrWhiteSpace(profile.ProductionModel))
        {
            farmingMethods.Add(profile.ProductionModel.Trim());
        }
        if (!string.IsNullOrWhiteSpace(profile.ProductionScale))
        {
            farmingMethods.Add($"Quy mô: {profile.ProductionScale.Trim()}");
        }
        if (farmingMethods.Count == 0)
        {
            farmingMethods.Add("Quy chuẩn canh tác an toàn địa phương");
        }

        // AF3: Storage infrastructure fallback
        var storageInfrastructure = !string.IsNullOrWhiteSpace(profile.ShortDescription) &&
                                    (profile.ShortDescription.Contains("kho", StringComparison.OrdinalIgnoreCase) ||
                                     profile.ShortDescription.Contains("bảo quản", StringComparison.OrdinalIgnoreCase) ||
                                     profile.ShortDescription.Contains("lạnh", StringComparison.OrdinalIgnoreCase))
            ? profile.ShortDescription
            : "Hệ thống nhà kho phân loại, đóng gói đạt chuẩn chất lượng nông sản và sẵn sàng liên kết chuỗi kho lạnh theo yêu cầu vận chuyển đường dài.";

        // Crop Cycles & Seasons
        var cropCycles = (profile.CropSeasons ?? [])
            .OrderByDescending(cs => cs.PlantingDate)
            .Select(cs => new SupplierCropSeasonDto(
                CropSeasonId: cs.CropSeasonId,
                SeasonName: cs.SeasonName,
                CropVariety: cs.CropVariety,
                ProductId: cs.ProductId,
                ProductName: cs.Product?.ProductName ?? "Nông sản mùa vụ",
                CultivatedArea: cs.CultivatedArea,
                PlantingDate: cs.PlantingDate,
                EstimatedHarvestDate: cs.EstimatedHarvestDate,
                ActualHarvestDate: cs.ActualHarvestDate,
                EstimatedYield: cs.EstimatedYield,
                ActualYield: cs.ActualYield,
                YieldUnit: cs.YieldUnit,
                Status: cs.Status,
                CultivationLogs: (cs.CultivationLogs ?? [])
                    .OrderBy(l => l.ActionDate)
                    .Select(l => new SupplierCultivationLogDto(
                        CultivationLogId: l.CultivationLogId,
                        ActionDate: l.ActionDate,
                        ActionType: l.ActionType,
                        DetailDescription: l.DetailDescription,
                        InputMaterial: l.InputMaterial,
                        Dosage: l.Dosage,
                        PerformedBy: l.PerformedBy,
                        ImageUrl: l.ImageUrl))
                    .ToList()))
            .ToList();

        // Available Inventory & Traceability Batches
        var availableInventory = new List<SupplierInventoryItemDto>();
        var traceabilityBatches = new List<SupplierTraceabilityBatchDto>();

        foreach (var prod in profile.Products ?? [])
        {
            var isApprovedAndPublished = string.Equals(prod.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase) && prod.IsPublished;
            var primaryImage = prod.ProductImages.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                               ?? prod.ProductImages.FirstOrDefault()?.ImageUrl;

            var availableQty = (prod.HarvestBatches ?? [])
                .Where(b => string.Equals(b.Status, "InStock", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(b.Status, "Available", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(b.Status, "Harvested", StringComparison.OrdinalIgnoreCase))
                .Sum(b => b.AvailableQuantity);

            if (isApprovedAndPublished)
            {
                availableInventory.Add(new SupplierInventoryItemDto(
                    ProductId: prod.ProductId,
                    ProductName: prod.ProductName,
                    Unit: prod.Unit,
                    BaseRetailPrice: prod.BaseRetailPrice,
                    WholesalePrice: prod.WholesalePrice,
                    WholesaleMinQuantity: prod.WholesaleMinQuantity,
                    AvailableQuantity: availableQty,
                    OriginProvince: prod.OriginProvince ?? profile.ProvinceCode,
                    PrimaryImageUrl: primaryImage));
            }

            foreach (var batch in prod.HarvestBatches ?? [])
            {
                var qr = batch.TraceabilityQrcode;
                var logsCount = batch.CropSeason?.CultivationLogs?.Count ?? 0;

                traceabilityBatches.Add(new SupplierTraceabilityBatchDto(
                    HarvestBatchId: batch.HarvestBatchId,
                    BatchCode: batch.BatchCode,
                    ProductId: prod.ProductId,
                    ProductName: prod.ProductName,
                    ActualHarvestDate: batch.ActualHarvestDate ?? batch.EstimatedHarvestDate,
                    AvailableQuantity: batch.AvailableQuantity,
                    Unit: prod.Unit,
                    QrContentString: qr?.QrcontentString,
                    TargetUrl: qr?.TargetUrl,
                    ScanCount: qr?.ScanCount ?? 0,
                    CultivationLogCount: logsCount));
            }
        }

        return new SupplierCapacityProfileDto(
            FarmerProfileId: profile.FarmerProfileId,
            UserId: profile.UserId,
            FarmName: profile.FarmName,
            RepresentativeName: profile.RepresentativeName,
            LegalName: profile.RepresentativeName,
            AvatarUrl: profile.User?.AvatarUrl,
            Email: profile.User?.Email ?? string.Empty,
            PhoneNumber: profile.User?.PhoneNumber,
            ProvinceCode: profile.ProvinceCode,
            ProvinceName: ResolveProvinceName(profile.ProvinceCode, profile.Address),
            Address: profile.Address,
            CultivationZone: profile.CultivationZone ?? "Vùng canh tác chưa phân vùng đặc thù",
            GpsCoordinates: profile.Gpscoordinates,
            ProductionModel: profile.ProductionModel ?? "Hộ nông dân liên kết / Hợp tác xã",
            CultivatedArea: profile.CultivatedArea,
            ProductionScale: profile.ProductionScale ?? "Quy mô canh tác tiêu chuẩn",
            ShortDescription: profile.ShortDescription ?? "Nông trại chuyên canh cung cấp nông sản chất lượng cao cho thị trường bán lẻ và đơn hàng sỉ B2B.",
            StorageInfrastructure: storageInfrastructure,
            FarmingMethods: farmingMethods.ToList(),
            VerificationStatus: profile.VerificationStatus,
            IsVerified: isVerified,
            ApprovedAt: profile.ApprovedAt,
            ReputationScore: profile.ReputationScore,
            CropCycles: cropCycles,
            AvailableInventory: availableInventory,
            Certificates: certificates,
            TraceabilityBatches: traceabilityBatches.OrderByDescending(b => b.ActualHarvestDate).Take(15).ToList(),
            PerformanceStats: performanceStats,
            RecentReviews: recentReviews);
    }

    private static string ResolveProvinceName(string provinceCode, string address)
    {
        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return address;
        }

        return provinceCode switch
        {
            "49" => "Lâm Đồng",
            "82" => "Tiền Giang",
            "89" => "An Giang",
            "33" => "Hưng Yên",
            "66" => "Đắk Lắk",
            "79" => "TP. Hồ Chí Minh",
            "01" => "Hà Nội",
            "48" => "Đà Nẵng",
            "92" => "Cần Thơ",
            "74" => "Bình Dương",
            "75" => "Đồng Nai",
            "77" => "Bà Rịa - Vũng Tàu",
            _ => address.Split(',').LastOrDefault()?.Trim() ?? provinceCode
        };
    }
}
