using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class CartRepository(AgriSustainContext context) : ICartRepository
{
    public Task<Cart?> GetOwnedAsync(long userId, CancellationToken cancellationToken) =>
        context.Carts.Include(x => x.CartItems).ThenInclude(x => x.HarvestBatch)
            .Include(x => x.CartItems).ThenInclude(x => x.Product).ThenInclude(x => x.Category)
            .Include(x => x.CartItems).ThenInclude(x => x.Product).ThenInclude(x => x.ProductImages)
            .Include(x => x.CartItems).ThenInclude(x => x.Product)
                .ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<HarvestBatch?> GetOfferAsync(long productId, long batchId,
        CancellationToken cancellationToken) => context.HarvestBatches
        .Include(x => x.Product).ThenInclude(x => x.Category)
        .Include(x => x.Product).ThenInclude(x => x.ProductImages)
        .Include(x => x.Product).ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.ProductId == productId
            && x.HarvestBatchId == batchId, cancellationToken);

    public async Task<IReadOnlyList<HarvestBatch>> GetOffersAsync(long productId,
        CancellationToken cancellationToken) => await context.HarvestBatches
        .Include(x => x.Product).ThenInclude(x => x.Category)
        .Include(x => x.Product).ThenInclude(x => x.ProductImages)
        .Include(x => x.Product).ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
        .Where(x => x.ProductId == productId && x.AvailableQuantity > 0)
        .AsSplitQuery().ToListAsync(cancellationToken);

    public void Add(Cart cart) => context.Carts.Add(cart);
    public void Remove(CartItem item) => context.CartItems.Remove(item);
}


