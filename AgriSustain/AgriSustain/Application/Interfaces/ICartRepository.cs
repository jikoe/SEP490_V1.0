using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetOwnedAsync(long userId, CancellationToken cancellationToken);
    Task<HarvestBatch?> GetOfferAsync(long productId, long batchId,
        CancellationToken cancellationToken);
    void Add(Cart cart);
    void Remove(CartItem item);
}

