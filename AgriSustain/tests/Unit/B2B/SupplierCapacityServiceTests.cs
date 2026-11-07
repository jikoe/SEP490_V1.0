using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.B2B;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.B2B;

public sealed class SupplierCapacityServiceTests
{
    private sealed class FakeSupplierCapacityRepository : ISupplierCapacityRepository
    {
        public readonly Dictionary<long, FarmerProfile> Profiles = new();
        public readonly Dictionary<long, FarmCertificate> Certificates = new();
        public readonly Dictionary<long, List<CultivationLog>> SeasonLogs = new();
        public SupplierPerformanceStatsDto Stats = new(10, 9, 90.0, 1, 10.0, 4.8, 5);
        public List<SupplierReviewDto> Reviews = new();

        public Task<FarmerProfile?> GetFarmerProfileWithDetailsAsync(long farmerProfileId, CancellationToken cancellationToken)
        {
            Profiles.TryGetValue(farmerProfileId, out var profile);
            return Task.FromResult(profile);
        }

        public Task<FarmerProfile?> GetFarmerProfileByUserIdAsync(long userId, CancellationToken cancellationToken)
        {
            var profile = Profiles.Values.FirstOrDefault(p => p.UserId == userId);
            return Task.FromResult(profile);
        }

        public Task<IReadOnlyList<FarmerProfile>> GetSuppliersDirectoryAsync(string? keyword, string? province, CancellationToken cancellationToken)
        {
            var query = Profiles.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p => p.FarmName.Contains(keyword) || p.RepresentativeName.Contains(keyword));
            }
            if (!string.IsNullOrWhiteSpace(province))
            {
                query = query.Where(p => p.ProvinceCode == province);
            }
            return Task.FromResult<IReadOnlyList<FarmerProfile>>(query.ToList());
        }

        public Task<FarmCertificate?> GetCertificateDossierAsync(long certificateId, CancellationToken cancellationToken)
        {
            Certificates.TryGetValue(certificateId, out var cert);
            return Task.FromResult(cert);
        }

        public Task<IReadOnlyList<CultivationLog>> GetSeasonCultivationLogsAsync(long cropSeasonId, CancellationToken cancellationToken)
        {
            if (SeasonLogs.TryGetValue(cropSeasonId, out var logs))
            {
                return Task.FromResult<IReadOnlyList<CultivationLog>>(logs);
            }
            return Task.FromResult<IReadOnlyList<CultivationLog>>(new List<CultivationLog>());
        }

        public Task<SupplierPerformanceStatsDto> GetFarmerPerformanceStatsAsync(long farmerProfileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Stats);
        }

        public Task<IReadOnlyList<SupplierReviewDto>> GetRecentReviewsAsync(long farmerProfileId, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SupplierReviewDto>>(Reviews.Take(limit).ToList());
        }
    }

    [Fact]
    public async Task GetCapacityProfileAsync_ValidFarmer_ReturnsFullCapacityProfile()
    {
        var repo = new FakeSupplierCapacityRepository();
        var profile = new FarmerProfile
        {
            FarmerProfileId = 1,
            UserId = 10,
            FarmName = "Nông Trại Hữu Cơ Đà Lạt",
            RepresentativeName = "Nguyễn Văn Nông",
            Address = "123 Đa Quý, Phường Xuân Thọ, Đà Lạt",
            ProvinceCode = "49",
            CultivationZone = "Cao nguyên Lâm Viên",
            CultivatedArea = 5.5m,
            ProductionModel = "Cooperative",
            ProductionScale = "5.5 ha",
            VerificationStatus = "Approved",
            ReputationScore = 95.0m,
            User = new User { UserId = 10, Email = "farmer1@agrisustain.vn", FullName = "Nguyễn Văn Nông", PhoneNumber = "0987654321" },
            FarmCertificates = new List<FarmCertificate>
            {
                new()
                {
                    FarmCertificateId = 101,
                    CertificateType = "VietGAP",
                    CertificateNumber = "VG-2026-001",
                    IssuedBy = "Trung tâm Khảo kiểm nghiệm Lâm Đồng",
                    IssueDate = new DateOnly(2025, 1, 1),
                    ExpiryDate = new DateOnly(2027, 1, 1),
                    VerificationStatus = "Verified",
                    DocumentUrl = "https://example.com/certs/vietgap.pdf"
                }
            },
            CropSeasons = new List<CropSeason>
            {
                new()
                {
                    CropSeasonId = 201,
                    SeasonName = "Dâu Tây Xuân 2026",
                    CropVariety = "Dâu tây New Zealand",
                    CultivatedArea = 2.0m,
                    PlantingDate = new DateOnly(2026, 1, 15),
                    EstimatedHarvestDate = new DateOnly(2026, 4, 30),
                    EstimatedYield = 4000m,
                    YieldUnit = "kg",
                    Status = "InProgress",
                    Product = new Product { ProductId = 301, ProductName = "Dâu Tây Đà Lạt Cao Cấp" }
                }
            },
            Products = new List<Product>
            {
                new()
                {
                    ProductId = 301,
                    ProductName = "Dâu Tây Đà Lạt Cao Cấp",
                    Unit = "kg",
                    BaseRetailPrice = 180000m,
                    WholesalePrice = 130000m,
                    WholesaleMinQuantity = 50m,
                    ApprovalStatus = "Approved",
                    IsPublished = true,
                    HarvestBatches = new List<HarvestBatch>
                    {
                        new()
                        {
                            HarvestBatchId = 401,
                            BatchCode = "BATCH-DAU-001",
                            AvailableQuantity = 500m,
                            Status = "InStock",
                            ActualHarvestDate = new DateOnly(2026, 3, 1),
                            TraceabilityQrcode = new TraceabilityQrcode
                            {
                                QrcontentString = "TRACE-DAU-001",
                                TargetUrl = "https://agrisustain.vn/trace/BATCH-DAU-001",
                                ScanCount = 28
                            }
                        }
                    }
                }
            }
        };

        repo.Profiles[1] = profile;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCapacityProfileAsync(1, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Nông Trại Hữu Cơ Đà Lạt", result.Data!.FarmName);
        Assert.True(result.Data.IsVerified);
        Assert.Equal("Approved", result.Data.VerificationStatus);
        Assert.Equal(5.5m, result.Data.CultivatedArea);
        Assert.Single(result.Data.Certificates);
        Assert.True(result.Data.Certificates[0].IsValid);
        Assert.Single(result.Data.CropCycles);
        Assert.Single(result.Data.AvailableInventory);
        Assert.Equal(500m, result.Data.AvailableInventory[0].AvailableQuantity);
        Assert.Equal(130000m, result.Data.AvailableInventory[0].WholesalePrice);
        Assert.Single(result.Data.TraceabilityBatches);
        Assert.Equal(90.0, result.Data.PerformanceStats.FulfillmentRate);
    }

    [Fact]
    public async Task GetCapacityProfileAsync_IncompleteCapacityInfo_AF3_GracefulFallbacks()
    {
        var repo = new FakeSupplierCapacityRepository();
        var profile = new FarmerProfile
        {
            FarmerProfileId = 2,
            UserId = 20,
            FarmName = "Vườn Rau Anh Ba",
            RepresentativeName = "Trần Văn Ba",
            Address = "Huyện Cái Bè, Tiền Giang",
            ProvinceCode = "82",
            CultivatedArea = null, // Incomplete
            CultivationZone = null, // Incomplete
            ProductionModel = null, // Incomplete
            ProductionScale = null, // Incomplete
            ShortDescription = null, // Incomplete
            VerificationStatus = "PendingReview",
            User = new User { UserId = 20, Email = "ba@example.com" }
        };
        repo.Profiles[2] = profile;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCapacityProfileAsync(2, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data!.CultivatedArea);
        Assert.NotNull(result.Data.StorageInfrastructure);
        Assert.NotNull(result.Data.CultivationZone);
        Assert.NotEmpty(result.Data.FarmingMethods);
        Assert.Empty(result.Data.Certificates);
        Assert.Empty(result.Data.CropCycles);
        Assert.Empty(result.Data.AvailableInventory);
    }

    [Fact]
    public async Task GetCapacityProfileAsync_UnverifiedFarm_AF4_SetsIsVerifiedFalse()
    {
        var repo = new FakeSupplierCapacityRepository();
        var profile = new FarmerProfile
        {
            FarmerProfileId = 3,
            UserId = 30,
            FarmName = "Nông trại Mới Thành Lập",
            RepresentativeName = "Lê Hoàng Lúa",
            Address = "Chợ Mới, An Giang",
            ProvinceCode = "89",
            VerificationStatus = "PendingReview",
            User = new User { UserId = 30, Email = "lua@example.com" }
        };
        repo.Profiles[3] = profile;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCapacityProfileAsync(3, default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsVerified);
        Assert.Equal("PendingReview", result.Data.VerificationStatus);
    }

    [Fact]
    public async Task GetCapacityProfileAsync_NoCertificates_AF5_ReturnsEmptyCertList()
    {
        var repo = new FakeSupplierCapacityRepository();
        var profile = new FarmerProfile
        {
            FarmerProfileId = 4,
            UserId = 40,
            FarmName = "Vườn Cam Xoàn",
            RepresentativeName = "Phạm Văn Cam",
            Address = "Khoái Châu, Hưng Yên",
            ProvinceCode = "33",
            VerificationStatus = "Approved",
            FarmCertificates = new List<FarmCertificate>(),
            User = new User { UserId = 40, Email = "cam@example.com" }
        };
        repo.Profiles[4] = profile;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCapacityProfileAsync(4, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Certificates);
    }

    [Fact]
    public async Task GetCapacityProfileAsync_NoPriorReviewsOrOrders_AF6_ReturnsSafeZeroStats()
    {
        var repo = new FakeSupplierCapacityRepository
        {
            Stats = new SupplierPerformanceStatsDto(0, 0, 100.0, 0, 0.0, 5.0, 0),
            Reviews = new List<SupplierReviewDto>()
        };
        var profile = new FarmerProfile
        {
            FarmerProfileId = 5,
            UserId = 50,
            FarmName = "Nông trại Cà Phê Mới",
            RepresentativeName = "Hoàng Minh Cà Phê",
            Address = "Buôn Ma Thuột, Đắk Lắk",
            ProvinceCode = "66",
            VerificationStatus = "Approved",
            User = new User { UserId = 50, Email = "caphe@example.com" }
        };
        repo.Profiles[5] = profile;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCapacityProfileAsync(5, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.PerformanceStats.TotalOrders);
        Assert.Equal(0, result.Data.PerformanceStats.DisputeCount);
        Assert.Equal(0, result.Data.PerformanceStats.ReviewCount);
        Assert.Empty(result.Data.RecentReviews);
    }

    [Fact]
    public async Task GetCertificateDossierAsync_AF1_ReturnsFullDossier()
    {
        var repo = new FakeSupplierCapacityRepository();
        var cert = new FarmCertificate
        {
            FarmCertificateId = 999,
            FarmerProfileId = 1,
            CertificateType = "GlobalGAP",
            CertificateNumber = "GG-2026-9999",
            IssuedBy = "Tổ chức Chứng nhận Quốc tế BVQI",
            IssueDate = new DateOnly(2025, 6, 1),
            ExpiryDate = new DateOnly(2028, 6, 1),
            VerificationStatus = "Verified",
            DocumentUrl = "https://agrisustain.vn/storage/certs/gg999.pdf",
            ReviewedAt = new DateTime(2025, 6, 5, 9, 30, 0, DateTimeKind.Utc),
            FarmerProfile = new FarmerProfile { FarmName = "Nông Trại Hữu Cơ Đà Lạt" },
            ReviewedByUser = new User { FullName = "Huy - Chuyên Viên Thẩm Định" }
        };
        repo.Certificates[999] = cert;
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCertificateDossierAsync(999, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("GG-2026-9999", result.Data!.CertificateNumber);
        Assert.Equal("GlobalGAP", result.Data.CertificateType);
        Assert.True(result.Data.IsValid);
        Assert.Equal("Huy - Chuyên Viên Thẩm Định", result.Data.ReviewedByOfficerName);
        Assert.Equal("Nông Trại Hữu Cơ Đà Lạt", result.Data.FarmName);
    }

    [Fact]
    public async Task GetCertificateDossierAsync_NotFound_Returns404()
    {
        var repo = new FakeSupplierCapacityRepository();
        var service = new SupplierCapacityService(repo);

        var result = await service.GetCertificateDossierAsync(88888, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetSeasonCultivationLogsAsync_AF2_ReturnsLogsInOrder()
    {
        var repo = new FakeSupplierCapacityRepository();
        repo.SeasonLogs[500] = new List<CultivationLog>
        {
            new()
            {
                CultivationLogId = 1,
                CropSeasonId = 500,
                ActionDate = new DateTime(2026, 2, 1, 8, 0, 0),
                ActionType = "Làm đất & Gieo giống",
                DetailDescription = "Cày ải đất, phơi đất 5 ngày và bón lót phân vi sinh trùn quế",
                InputMaterial = "Hạt giống F1 New Zealand",
                Dosage = "1500 cây/sào",
                PerformedBy = "Tổ làm đất A"
            },
            new()
            {
                CultivationLogId = 2,
                CropSeasonId = 500,
                ActionDate = new DateTime(2026, 2, 15, 9, 0, 0),
                ActionType = "Bón phân đợt 1",
                DetailDescription = "Bón thúc phân hữu cơ rong biển qua hệ thống tưới nhỏ giọt Israel",
                InputMaterial = "Phân hữu cơ Seasol",
                Dosage = "5 lít/ha",
                PerformedBy = "Kỹ thuật viên Nguyễn Văn A"
            }
        };

        var service = new SupplierCapacityService(repo);
        var result = await service.GetSeasonCultivationLogsAsync(500, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal("Làm đất & Gieo giống", result.Data[0].ActionType);
        Assert.Equal("Bón phân đợt 1", result.Data[1].ActionType);
    }

    [Fact]
    public async Task GetDirectoryAsync_FiltersByKeywordAndProvince()
    {
        var repo = new FakeSupplierCapacityRepository();
        repo.Profiles[1] = new FarmerProfile
        {
            FarmerProfileId = 1,
            UserId = 1,
            FarmName = "Nông Trại Hữu Cơ Đà Lạt",
            RepresentativeName = "Nguyễn Văn Nông",
            Address = "Đà Lạt, Lâm Đồng",
            ProvinceCode = "49",
            VerificationStatus = "Approved",
            ReputationScore = 95
        };
        repo.Profiles[2] = new FarmerProfile
        {
            FarmerProfileId = 2,
            UserId = 2,
            FarmName = "Vườn Xoài Cát Tiền Giang",
            RepresentativeName = "Trần Thị Mía",
            Address = "Cái Bè, Tiền Giang",
            ProvinceCode = "82",
            VerificationStatus = "Approved",
            ReputationScore = 88
        };

        var service = new SupplierCapacityService(repo);

        var resultAll = await service.GetDirectoryAsync(null, null, default);
        Assert.Equal(2, resultAll.Data!.Count);

        var resultLamDong = await service.GetDirectoryAsync(null, "49", default);
        Assert.Single(resultLamDong.Data!);
        Assert.Equal("Nông Trại Hữu Cơ Đà Lạt", resultLamDong.Data![0].FarmName);

        var resultKeyword = await service.GetDirectoryAsync("Xoài", null, default);
        Assert.Single(resultKeyword.Data!);
        Assert.Equal("Vườn Xoài Cát Tiền Giang", resultKeyword.Data![0].FarmName);
    }
}

