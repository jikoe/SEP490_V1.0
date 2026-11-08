using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ISupplierCapacityRepository
{
    Task<FarmerProfile?> GetFarmerProfileWithDetailsAsync(long farmerProfileId, CancellationToken cancellationToken);
    Task<FarmerProfile?> GetFarmerProfileByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FarmerProfile>> GetSuppliersDirectoryAsync(string? keyword, string? province, CancellationToken cancellationToken);
    Task<FarmCertificate?> GetCertificateDossierAsync(long certificateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CultivationLog>> GetSeasonCultivationLogsAsync(long cropSeasonId, CancellationToken cancellationToken);
    Task<SupplierPerformanceStatsDto> GetFarmerPerformanceStatsAsync(long farmerProfileId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupplierReviewDto>> GetRecentReviewsAsync(long farmerProfileId, int limit, CancellationToken cancellationToken);
}

