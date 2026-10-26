using AgriSustain.Application.DTOs.Moderation;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IProductModerationRepository
{
    Task<(int Total, IReadOnlyList<ProductModerationListItemDto> Items)> GetListAsync(
        string status,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProductModerationDetailDto?> GetDetailAsync(
        long productId,
        CancellationToken cancellationToken);

    Task<Product?> GetForModerationAsync(
        long productId,
        CancellationToken cancellationToken);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
