using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IComplaintRepository
{
    /// <summary>
    /// Filtered, paged complaint list. HasOverdueEvidence is evaluated at <paramref name="now"/> with the 48-hour window.
    /// </summary>
    /// <param name="query">Normalized filters and paging.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Total matches and the requested page.</returns>
    Task<(int Total, IReadOnlyList<ComplaintListItemDto> Items)> SearchAsync(
        ComplaintQuery query,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Distinct complaint reasons, sorted, for the reason filter (3).
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Reason texts.</returns>
    Task<IReadOnlyList<string>> GetReasonsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Read model; MaxRefundAmount is left at 0 for the service to compute.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="now">Current UTC time, used to flag overdue requests.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or null.</returns>
    Task<ComplaintDetailDto?> GetDetailAsync(
        long ticketId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tracked ticket with its seller order, parent order, payments with their refunds and
    /// evidence requests, ready for a decision.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The tracked entity graph, or null.</returns>
    Task<DisputeTicket?> GetForDecisionAsync(
        long ticketId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Complaints waiting for information where every open request is past its due time,
    /// oldest first.
    /// </summary>
    /// <param name="now">Current UTC time.</param>
    /// <param name="responseWindow">Fallback window for old requests without DueAt.</param>
    /// <param name="take">Batch size.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Complaint ids to decide.</returns>
    Task<IReadOnlyList<long>> GetOverdueTicketIdsAsync(
        DateTime now,
        TimeSpan responseWindow,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stored URL of an evidence file of the given complaint.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="evidenceId">Evidence id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>URL or null.</returns>
    Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Active users in any of the given roles (notification recipients).
    /// </summary>
    /// <param name="roles">Role names.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>User ids.</returns>
    Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    /// <summary>
    /// The buyer's saved default account (UC-AUTH-03), snapshotted onto a new refund.
    /// </summary>
    /// <param name="userId">Buyer id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The account, or null.</returns>
    Task<UserBankAccount?> GetDefaultBankAccountAsync(
        long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new refund request for the next SaveChanges.
    /// </summary>
    /// <param name="refund">Refund to insert.</param>
    void AddRefund(RefundTransaction refund);

    /// <summary>
    /// Stages an audit row.
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
