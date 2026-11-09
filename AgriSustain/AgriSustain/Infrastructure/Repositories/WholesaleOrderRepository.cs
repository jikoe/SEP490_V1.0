using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public class WholesaleOrderRepository(AgriSustainContext database) : IWholesaleOrderRepository
{
    private readonly AgriSustainContext _db = database;

    public async Task<Product?> GetWholesaleProductAsync(long productId, CancellationToken cancellationToken)
    {
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.FarmerProfile)
                .ThenInclude(f => f.User)
            .Include(p => p.FarmerProfile)
                .ThenInclude(f => f.FarmCertificates)
            .Include(p => p.ProductImages)
            .Include(p => p.HarvestBatches)
            .SingleOrDefaultAsync(p => p.ProductId == productId, cancellationToken);
    }

    public async Task<WholesaleBuyerProfile?> GetBuyerKycProfileAsync(long userId, CancellationToken cancellationToken)
    {
        return await _db.WholesaleBuyerProfiles
            .AsNoTracking()
            .Include(w => w.User)
            .SingleOrDefaultAsync(w => w.UserId == userId, cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(long userId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<HarvestBatch>> GetAvailableHarvestBatchesAsync(long productId, CancellationToken cancellationToken)
    {
        return await _db.HarvestBatches
            .Where(b => b.ProductId == productId &&
                        (b.Status == "InStock" || b.Status == "Available" || b.Status == "Harvested") &&
                        b.AvailableQuantity > 0)
            .OrderBy(b => b.ActualHarvestDate ?? b.EstimatedHarvestDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode, CancellationToken cancellationToken)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(o => o.PaymentTransactions)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
                    .ThenInclude(oi => oi.OrderItemBatchAllocations)
                        .ThenInclude(ba => ba.HarvestBatch)
            .Include(o => o.BuyerUser)
            .SingleOrDefaultAsync(o => o.OrderCode == orderCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetBuyerWholesaleOrdersAsync(long buyerUserId, CancellationToken cancellationToken)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.PaymentTransactions)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
                    .ThenInclude(oi => oi.OrderItemBatchAllocations)
                        .ThenInclude(ba => ba.HarvestBatch)
            .Include(o => o.BuyerUser)
            .Where(o => o.OrderType == "Wholesale");

        if (buyerUserId > 0)
        {
            var userOrders = await query
                .Where(o => o.BuyerUserId == buyerUserId)
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .ToListAsync(cancellationToken);

            if (userOrders.Count > 0)
            {
                return userOrders;
            }
        }

        return await query
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.OrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Order?> GetWholesaleOrderForPaymentAsync(string orderCode, CancellationToken cancellationToken)
    {
        return await _db.Orders
            .Include(o => o.PaymentTransactions)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrders)
                .ThenInclude(so => so.OrderItems)
                    .ThenInclude(oi => oi.OrderItemBatchAllocations)
                        .ThenInclude(ba => ba.HarvestBatch)
            .SingleOrDefaultAsync(o => o.OrderCode == orderCode, cancellationToken);
    }

    public async Task AddPaymentTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken)
    {
        _db.PaymentTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}

