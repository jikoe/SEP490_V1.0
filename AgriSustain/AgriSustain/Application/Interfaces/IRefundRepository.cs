using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IRefundRepository
{
    Task<(int Total, IReadOnlyList<RefundListItemDto> Items)> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken);

    Task<RefundDetailDto?> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tracked refund with its payment (and that payment's refunds) and its complaint's seller
    /// order together with the parent order's seller orders.
    /// </summary>
    Task<RefundTransaction?> GetForProcessingAsync(
        long refundId,
        CancellationToken cancellationToken);

    Task<bool> TransferReferenceExistsAsync(
        string transferReference,
        CancellationToken cancellationToken);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
