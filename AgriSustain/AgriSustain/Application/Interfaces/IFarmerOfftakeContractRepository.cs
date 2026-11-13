using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public sealed class OfftakeContext
{
    public int CompletedContractCount { get; init; }
    public bool NotificationFailed { get; init; }
    public required B2bcontract Contract { get; init; }
    public required List<CropSeason> Seasons { get; init; }
    public required List<HarvestBatch> Batches { get; init; }
    public List<PreOrderCampaign> PreOrderCommitments { get; init; } = [];
    public required List<ContractLine> OtherCommitments { get; init; }
    public required List<AuditLog> History { get; init; }
    public required List<FarmerPayout> Payouts { get; init; }
    public required List<DisputeTicket> Disputes { get; init; }
    public required List<RefundTransaction> Refunds { get; init; }
    public required List<User> Reviewers { get; init; }
}

public interface IFarmerOfftakeContractRepository
{
    Task<FarmerProfile?> FarmerAsync(long actor, CancellationToken ct);
    Task<object> SearchOwnedAsync(long actor, ContractQuery query, CancellationToken ct);
    Task<OfftakeContext?> LoadContextAsync(long actor, long id, bool update, CancellationToken ct);
    Task<FeatureResult<ContractActionResult>> SaveAtomicAsync(long actor, long id,
        Func<OfftakeContext, Task<FeatureResult<ContractActionResult>>> action, CancellationToken ct);
    void Record(AuditLog entry);
    void Queue(IEnumerable<Notification> notifications);
}
