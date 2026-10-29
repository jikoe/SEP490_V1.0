using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IComplaintRepository
{
    Task<(int Total, IReadOnlyList<ComplaintListItemDto> Items)> SearchAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetReasonsAsync(CancellationToken cancellationToken);

    /// <summary>Read model; MaxRefundAmount is left at 0 for the service to compute.</summary>
    Task<ComplaintDetailDto?> GetDetailAsync(
        long ticketId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tracked ticket with its seller order, parent order, payments with their refunds and
    /// evidence requests, ready for a decision.
    /// </summary>
    Task<DisputeTicket?> GetForDecisionAsync(
        long ticketId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Complaints waiting for information where every open request is past its due time,
    /// oldest first.
    /// </summary>
    Task<IReadOnlyList<long>> GetOverdueTicketIdsAsync(
        DateTime now,
        TimeSpan responseWindow,
        int take,
        CancellationToken cancellationToken);

    Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    void AddRefund(RefundTransaction refund);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
