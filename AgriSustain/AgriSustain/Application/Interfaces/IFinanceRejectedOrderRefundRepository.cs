using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFinanceRejectedOrderRefundRepository
{
    Task<bool> AuthorizedAsync(long userId, CancellationToken ct);
    Task<object> SearchAsync(RejectedOrderRefundQuery query, CancellationToken ct);
    Task<RejectedOrderRefundContext?> LoadAsync(long id, bool update, CancellationToken ct);
    Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct);
    Task<bool> BankEventUsedAsync(string eventKey, string reference, CancellationToken ct);
    Task<T> AtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct);
    void Audit(AuditLog row);
    void Notify(Notification row);
}
