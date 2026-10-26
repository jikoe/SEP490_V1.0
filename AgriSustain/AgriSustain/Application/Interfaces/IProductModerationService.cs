using AgriSustain.Application.DTOs.Moderation;

namespace AgriSustain.Application.Interfaces;

public interface IProductModerationService
{
    Task<ProductModerationListDto?> GetListAsync(
        string? keyword,
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProductModerationDetailDto?> GetDetailAsync(
        long productId,
        CancellationToken cancellationToken);

    Task<ProductModerationOutcome> ModerateAsync(
        long productId,
        bool approved,
        string? reason,
        CancellationToken cancellationToken);
}
