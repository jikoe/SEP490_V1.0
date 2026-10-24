using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class CheckoutRepository(AgriSustainContext context) : ICheckoutRepository
{
    public async Task<IReadOnlyList<CartItem>> SelectedItemsAsync(long userId,
        CancellationToken cancellationToken)
    {
        return await context.CartItems.Where(x => x.Cart.UserId == userId && x.IsSelected)
            .Include(x => x.Product).ThenInclude(x => x.Category)
            .Include(x => x.Product).ThenInclude(x => x.ProductImages)
            .Include(x => x.Product).ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
            .OrderBy(x => x.CartItemId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeliveryAddress>> AddressesAsync(long userId,
        CancellationToken cancellationToken)
    {
        return await context.DeliveryAddresses.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "Active")
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.DeliveryAddressId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HarvestBatch>> BatchesAsync(IReadOnlyList<long> productIds,
        CancellationToken cancellationToken)
    {
        return await context.HarvestBatches.Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.HarvestBatchId).ToListAsync(cancellationToken);
    }

    public async Task<long> PointsAsync(long userId, CancellationToken cancellationToken)
    {
        return await context.RewardPointEntries.Where(x => x.UserId == userId)
            .SumAsync(x => x.AvailableDelta ?? 0, cancellationToken);
    }

    public async Task<IReadOnlyList<Voucher>> VouchersAsync(DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await context.Vouchers.AsNoTracking().Where(x => x.IsActive
            && x.StartDate <= now.UtcDateTime && x.EndDate > now.UtcDateTime
            && (!x.UsageLimit.HasValue || x.UsedCount < x.UsageLimit))
            .OrderBy(x => x.VoucherCode).Take(100).ToListAsync(cancellationToken);
    }

    public Task<Voucher?> VoucherAsync(string code, CancellationToken cancellationToken)
    {
        return context.Vouchers.SingleOrDefaultAsync(x => x.VoucherCode == code, cancellationToken);
    }

    public Task<int> ReservedVoucherCountAsync(long voucherId, CancellationToken cancellationToken)
    {
        // Expired reservations still count until the cancellation transaction releases them.
        return context.VoucherRedemptions.CountAsync(x => x.VoucherId == voucherId
            && x.Status == "Reserved", cancellationToken);
    }

    public Task<RetailCheckoutQuote?> QuoteAsync(Guid id, long userId,
        CancellationToken cancellationToken)
    {
        return context.RetailCheckoutQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == id
            && x.BuyerUserId == userId, cancellationToken);
    }

    public async Task<Order?> ExistingOrderAsync(long userId, string requestKey, Guid quoteId,
        CancellationToken cancellationToken)
    {
        var requested = await context.Orders.SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.CheckoutRequestKey == requestKey, cancellationToken);
        return requested ?? await context.Orders.SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.RetailQuoteId == quoteId, cancellationToken);
    }

    public Task<Order?> GetOrderAsync(long userId, long orderId,
        CancellationToken cancellationToken)
    {
        return context.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.OrderId == orderId && x.OrderType == "Retail", cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ExpiredOrdersAsync(DateTimeOffset now, int limit,
        CancellationToken cancellationToken)
    {
        return await context.Orders.Where(x => x.OrderType == "Retail"
            && x.RetailQuoteId != null && x.OrderStatus == "PendingPayment"
            && x.ReservationExpiresAt <= now.UtcDateTime)
            .OrderBy(x => x.OrderId).Take(limit)
            .Include(x => x.SellerOrders).ThenInclude(x => x.OrderItems)
                .ThenInclude(x => x.OrderItemBatchAllocations).ThenInclude(x => x.HarvestBatch)
            .Include(x => x.VoucherRedemptions).Include(x => x.RewardPointEntries)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsCategoryArchivedAsync(long id, CancellationToken cancellationToken)
    {
        return context.CategorySettings.AnyAsync(x => x.CategoryId == id && x.IsArchived,
            cancellationToken);
    }

    public void AddQuote(RetailCheckoutQuote quote) => context.RetailCheckoutQuotes.Add(quote);

    public void AddOrder(Order order) => context.Orders.Add(order);

    public void RemoveCartItems(IEnumerable<CartItem> items)
    {
        context.CartItems.RemoveRange(items);
    }

    public void AddMovement(InventoryMovement movement) => context.InventoryMovements.Add(movement);

    public void AddRewardEntry(RewardPointEntry entry) => context.RewardPointEntries.Add(entry);
}
