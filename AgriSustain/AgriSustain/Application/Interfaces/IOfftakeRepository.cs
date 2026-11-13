using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IOfftakeRepository
{
    Task<long?> ApprovedBuyerAsync(long userId, CancellationToken ct);
    Task<IReadOnlyList<OfftakeProduct>> CatalogAsync(string? keyword, CancellationToken ct);
    Task<OfftakeProduct?> FindProductAsync(long farmerId, long productId, CancellationToken ct);
    Task<B2bcontract?> FindSubmissionAsync(long buyerId, string code, CancellationToken ct);
    Task<B2bcontract?> FindManagedAsync(long buyerId, long contractId, CancellationToken ct);
    Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct);
    Task<long> FarmerUserIdAsync(long farmerId, CancellationToken ct);
    void AddNotification(Notification notification);
    Task<B2bcontract?> FindRevisionAsync(long buyerId, long contractId, CancellationToken ct);
    void Add(B2bcontract contract);
    Task<OfftakeTrackingPage> TrackAsync(long userId, bool orders, OfftakeTrackingQuery query,
        CancellationToken ct);
    Task<OfftakeTrackingDetail?> DetailAsync(long userId, bool orders, long id,
        CancellationToken ct);
    Task<OfftakeTrackingDetail?> FarmerDetailAsync(long userId, long id, CancellationToken ct);
}
