using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public sealed class FarmerPayoutContext
{
    public required FarmerProfile Farmer { get; init; }
    public UserBankAccount? Bank { get; init; }
    public required List<InvoiceLine> Lines { get; init; }
    public required List<PayoutItem> Claims { get; init; }
    public required List<DisputeTicket> Disputes { get; init; }
    public required List<RefundTransaction> Refunds { get; init; }
    public required List<CommissionPolicy> Policies { get; init; }
    public required Dictionary<long, decimal> InvoiceTotals { get; init; }
    public FarmerPayout? Payout { get; init; }
    public List<AuditLog> History { get; init; } = [];
    public bool NotificationFailed { get; init; }
}

public interface IFinanceFarmerPayoutRepository
{
    Task<bool> AuthorizedAsync(long actor, CancellationToken ct);
    Task<object> SearchOwnedAsync(FarmerPayoutQuery query, CancellationToken ct);
    Task<IReadOnlyList<long>> PendingAsync(CancellationToken ct);
    Task<object> FarmersAsync(CancellationToken ct);
    Task<FarmerPayoutContext?> LoadContextAsync(long farmerId, long? payoutId, bool update, CancellationToken ct);
    Task<FarmerPayout?> FindRequestAsync(string key, CancellationToken ct);
    Task<long?> FarmerForPayoutAsync(long payoutId, CancellationToken ct);
    Task<long?> FindOperationAsync(string operation, CancellationToken ct);
    Task<AuditLog?> FindTraceAsync(string trace, CancellationToken ct);
    Task<T> SaveAtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct);
    Task FlushAsync(CancellationToken ct);
    void Add(FarmerPayout payout);
    void Record(AuditLog entry);
    void Queue(IEnumerable<Notification> notices);
}

public interface IPayoutGateway
{
    PayoutTransferPlan? CreateQr(FarmerPayout payout);
    bool AuthenticateWebhook(string authorization, string alternateKey);
    Task<PayoutGatewayLookup> GetTransferStatusAsync(PayoutTransferPlan plan, CancellationToken ct);
}
