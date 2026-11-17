using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ICustomerComplaintRepository
{
    Task<PagedResponse<CustomerComplaintListItem>> SearchAsync(
        long buyerId, CustomerComplaintQuery query, DateTime now, CancellationToken ct);

    Task<DisputeTicket?> LoadOwnedAsync(long buyerId, long id, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> GetResponsesAsync(long buyerId, long id, CancellationToken ct);
    Task<bool> HasRequestAsync(string traceId, CancellationToken ct);
    Task<string?> GetOwnedEvidenceUrlAsync(long buyerId, long id, long evidenceId, CancellationToken ct);
    void AddResponse(AuditLog audit, IEnumerable<DisputeEvidence> evidence);
}

public interface IComplaintEmailDelivery
{
    Task DeliverAsync(long notificationId, CancellationToken ct);
}
