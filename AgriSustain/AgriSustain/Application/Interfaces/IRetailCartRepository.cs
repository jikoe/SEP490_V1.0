using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IRetailCartRepository
{
    Task<Cart?> GetOwnedAsync(long userId, CancellationToken cancellationToken);
    Task<HarvestBatch?> GetOfferAsync(long productId, long batchId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<HarvestBatch>> GetOffersAsync(long productId,
        CancellationToken cancellationToken);
    Task<Cart?> LoadOwnedCartAsync(long actorId, CancellationToken ct) => GetOwnedAsync(actorId, ct);
    Task<HarvestBatch?> LoadSaleContextAsync(long productId, long batchId, CancellationToken ct) => GetOfferAsync(productId, batchId, ct);
    Task SaveCartAsync(Cart? cart, IUnitOfWork unitOfWork, CancellationToken ct) => unitOfWork.CommitAsync(ct);
    void Add(Cart cart);
    void Remove(CartItem item);
}

