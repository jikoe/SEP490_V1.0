using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IRefundRepository
{
    /// <summary>
    /// Filtered, paged refund list.
    /// </summary>
    /// <param name="query">Normalized filters and paging.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Total matches and the requested page.</returns>
    Task<(int Total, IReadOnlyList<RefundListItemDto> Items)> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read model of one refund with its source, balances and history.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or null.</returns>
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

    /// <summary>
    /// BR-RF-02: whether a bank transaction ID is already recorded on any refund.
    /// </summary>
    /// <param name="transferReference">Bank transaction ID.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>True when it is already used.</returns>
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

    /// <summary>
    /// Stages a history row.
    /// </summary>
    /// <param name="audit">Row to insert.</param>
    void AddAudit(AuditLog audit);

    /// <summary>
    /// Stages a notification row.
    /// </summary>
    /// <param name="notification">Row to insert.</param>
    void AddNotification(Notification notification);

    /// <summary>
    /// Commits every staged and tracked change in one database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
