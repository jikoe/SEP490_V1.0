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

    /// <summary>
    /// The cancelled seller order of a Farmer-rejection refund (RequestKey farmer-reject:{id}:...),
    /// with its order and farm, or null for a complaint refund.
    /// </summary>
    Task<SellerOrder?> GetFarmerRejectedSellerOrderAsync(
        RefundTransaction refund,
        CancellationToken cancellationToken);

    /// <summary>Time of the latest history row with this action for the refund, or null.</summary>
    Task<DateTime?> GetLastActionAtAsync(
        long refundId,
        string action,
        CancellationToken cancellationToken);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
