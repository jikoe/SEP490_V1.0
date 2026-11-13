using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class CheckoutRepository(AgriSustainContext context) : ICheckoutRepository
{
    /// <summary>
    /// Selected lines of the customer's cart with everything the sellability checks read.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Lines.</returns>
    public async Task<IReadOnlyList<CartItem>> SelectedItemsAsync(long userId,
        CancellationToken cancellationToken)
    {
        return await context.CartItems.Where(x => x.Cart.UserId == userId && x.IsSelected)
            .Include(x => x.Product).ThenInclude(x => x.Category)
            .Include(x => x.Product).ThenInclude(x => x.ProductImages)
            .Include(x => x.Product).ThenInclude(x => x.FarmerProfile).ThenInclude(x => x.User)
            .OrderBy(x => x.CartItemId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Active addresses, default first.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Addresses.</returns>
    public async Task<IReadOnlyList<DeliveryAddress>> AddressesAsync(long userId,
        CancellationToken cancellationToken)
    {
        return await context.DeliveryAddresses.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == "Active")
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.DeliveryAddressId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tracked batches of the products.
    /// </summary>
    /// <param name="productIds">Products.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Batches.</returns>
    public async Task<IReadOnlyList<HarvestBatch>> BatchesAsync(IReadOnlyList<long> productIds,
        CancellationToken cancellationToken)
    {
        return await context.HarvestBatches.Where(x => productIds.Contains(x.ProductId))
            .OrderBy(x => x.HarvestBatchId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Sum of the available delta of the point ledger.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Balance.</returns>
    public async Task<long> PointsAsync(long userId, CancellationToken cancellationToken)
    {
        return await context.RewardPointEntries.Where(x => x.UserId == userId)
            .SumAsync(x => x.AvailableDelta ?? 0, cancellationToken);
    }

    /// <summary>
    /// Currently usable vouchers, by code.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Vouchers.</returns>
    public async Task<IReadOnlyList<Voucher>> VouchersAsync(DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await context.Vouchers.AsNoTracking().Where(x => x.IsActive
            && x.StartDate <= now.UtcDateTime && x.EndDate > now.UtcDateTime
            && (!x.UsageLimit.HasValue || x.UsedCount < x.UsageLimit))
            .OrderBy(x => x.VoucherCode).Take(100).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Tracked voucher by code.
    /// </summary>
    /// <param name="code">Code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher or null.</returns>
    public Task<Voucher?> VoucherAsync(string code, CancellationToken cancellationToken)
    {
        return context.Vouchers.SingleOrDefaultAsync(x => x.VoucherCode == code, cancellationToken);
    }

    /// <summary>
    /// Reserved redemptions of the voucher; expired holds still count until the expiry job releases them.
    /// </summary>
    /// <param name="voucherId">Voucher.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    public Task<int> ReservedVoucherCountAsync(long voucherId, CancellationToken cancellationToken)
    {
        // Expired reservations still count until the cancellation transaction releases them.
        return context.VoucherRedemptions.CountAsync(x => x.VoucherId == voucherId
            && x.Status == "Reserved", cancellationToken);
    }

    /// <summary>
    /// Redemptions of the customer not Released, grouped by voucher (a released hold gives the use back).
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher id → uses.</returns>
    public async Task<IReadOnlyDictionary<long, int>> CustomerVoucherUsesAsync(long userId,
        CancellationToken cancellationToken)
    {
        // A released hold gives the use back (BR-PV-10); a refunded paid order does not.
        return await context.VoucherRedemptions.AsNoTracking()
            .Where(x => x.Order.BuyerUserId == userId && x.Status != "Released")
            .GroupBy(x => x.VoucherId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    /// <summary>
    /// Quote by id and owner.
    /// </summary>
    /// <param name="id">Quote id.</param>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Quote or null.</returns>
    public Task<RetailCheckoutQuote?> QuoteAsync(Guid id, long userId,
        CancellationToken cancellationToken)
    {
        return context.RetailCheckoutQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == id
            && x.BuyerUserId == userId, cancellationToken);
    }

    /// <summary>
    /// Looks up by request key first, then by quote id.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="requestKey">Key.</param>
    /// <param name="quoteId">Quote id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Order or null.</returns>
    public async Task<Order?> ExistingOrderAsync(long userId, string requestKey, Guid quoteId,
        CancellationToken cancellationToken)
    {
        var requested = await context.Orders.SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.CheckoutRequestKey == requestKey, cancellationToken);
        return requested ?? await context.Orders.SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.RetailQuoteId == quoteId, cancellationToken);
    }

    /// <summary>
    /// Untracked retail order of the customer.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="orderId">Order id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Order or null.</returns>
    public Task<Order?> GetOrderAsync(long userId, long orderId,
        CancellationToken cancellationToken)
    {
        return context.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.BuyerUserId == userId
            && x.OrderId == orderId && x.OrderType == "Retail", cancellationToken);
    }

    /// <summary>
    /// PendingPayment checkout orders past ReservationExpiresAt, oldest first.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="limit">Batch size.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Orders.</returns>
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

    /// <summary>
    /// Archived flag from CategorySettings.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when archived.</returns>
    public Task<bool> IsCategoryArchivedAsync(long id, CancellationToken cancellationToken)
    {
        return context.CategorySettings.AnyAsync(x => x.CategoryId == id && x.IsArchived,
            cancellationToken);
    }

    /// <summary>
    /// Adds the quote to the context.
    /// </summary>
    /// <param name="quote">Quote.</param>
    public void AddQuote(RetailCheckoutQuote quote) => context.RetailCheckoutQuotes.Add(quote);

    /// <summary>
    /// Adds the order graph to the context.
    /// </summary>
    /// <param name="order">Order.</param>
    public void AddOrder(Order order) => context.Orders.Add(order);

    /// <summary>
    /// Removes cart lines from the context.
    /// </summary>
    /// <param name="items">Lines.</param>
    public void RemoveCartItems(IEnumerable<CartItem> items)
    {
        context.CartItems.RemoveRange(items);
    }

    /// <summary>
    /// Adds the movement to the context.
    /// </summary>
    /// <param name="movement">Movement.</param>
    public void AddMovement(InventoryMovement movement) => context.InventoryMovements.Add(movement);

    /// <summary>
    /// Adds the ledger entry to the context.
    /// </summary>
    /// <param name="entry">Entry.</param>
    public void AddRewardEntry(RewardPointEntry entry) => context.RewardPointEntries.Add(entry);
}
