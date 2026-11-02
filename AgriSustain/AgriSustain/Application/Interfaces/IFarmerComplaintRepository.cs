using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFarmerComplaintRepository
{
    Task<FarmerProfile?> GetFarmerAsync(long actor, CancellationToken ct);
    Task<FarmerComplaintPage> SearchOwnedAsync(long actor, FarmerComplaintQuery query, DateTime now, CancellationToken ct);
    Task<DisputeTicket?> LoadOwnedContextAsync(long actor, long id, bool forUpdate, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> GetResponsesAsync(long id, CancellationToken ct);
    Task<bool> HasRequestAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<long>> GetSupportRecipientsAsync(long? assigned, CancellationToken ct);
    Task<bool> HasFailedNotificationsAsync(long id, CancellationToken ct);
    void SaveResponseAsync(AuditLog response, IEnumerable<DisputeEvidence> files);
    void QueueSupportNotifications(IEnumerable<Notification> notifications);
    Task<IReadOnlyList<long>> PendingNotificationsAsync(string? eventKey, DateTime now, CancellationToken ct);
    Task<Notification?> ClaimNotificationAsync(long id, DateTime now, CancellationToken ct);
    Task RecordChannelResultAsync(Notification notification, bool sent, DateTime now, CancellationToken ct);
}

public interface IFarmerComplaintFiles
{
    Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct);
    Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct);
    string? Resolve(long actor, string url);
}
