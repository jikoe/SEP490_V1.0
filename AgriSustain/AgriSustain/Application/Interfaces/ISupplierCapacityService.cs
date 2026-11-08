using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.B2B;

namespace AgriSustain.Application.Interfaces;

public interface ISupplierCapacityService
{
    Task<Result<SupplierCapacityProfileDto>> GetCapacityProfileAsync(long farmerProfileId, CancellationToken cancellationToken);
    Task<Result<SupplierCapacityProfileDto>> GetCapacityProfileByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<SupplierCapacityDirectoryItemDto>>> GetDirectoryAsync(string? keyword, string? province, CancellationToken cancellationToken);
    Task<Result<SupplierCertificateDossierDto>> GetCertificateDossierAsync(long certificateId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<SupplierCultivationLogDto>>> GetSeasonCultivationLogsAsync(long cropSeasonId, CancellationToken cancellationToken);
}

