using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class OrderFeedbackRepository(AgriSustainContext context) : IOrderFeedbackRepository
{
    public Task<Order?> GetOwnedOrderAsync(
        long userId, long orderId, CancellationToken cancellationToken) =>
        context.Orders.Where(order => order.BuyerUserId == userId && order.OrderId == orderId
                && order.OrderType == "Retail")
            .Include(order => order.SellerOrders).ThenInclude(seller => seller.FarmerProfile)
            .Include(order => order.SellerOrders).ThenInclude(seller => seller.DisputeTickets)
            .Include(order => order.SellerOrders).ThenInclude(seller => seller.OrderStatusHistories)
            .Include(order => order.SellerOrders).ThenInclude(seller => seller.OrderItems)
                .ThenInclude(item => item.ProductReview)
            .Include(order => order.SellerOrders).ThenInclude(seller => seller.OrderItems)
                .ThenInclude(item => item.Product).ThenInclude(product => product.Category)
            .AsSplitQuery().SingleOrDefaultAsync(cancellationToken);

    public Task<Dictionary<long, string>> GetImagesAsync(
        long[] productIds, CancellationToken cancellationToken) =>
        context.ProductImages.AsNoTracking().Where(image => productIds.Contains(image.ProductId))
            .GroupBy(image => image.ProductId)
            .Select(group => new
            {
                Id = group.Key,
                Url = group.OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder).ThenBy(image => image.ProductImageId)
                    .Select(image => image.ImageUrl).First()
            }).ToDictionaryAsync(image => image.Id, image => image.Url, cancellationToken);

    public void Add(ProductReview review) => context.ProductReviews.Add(review);
    public void Add(DisputeTicket complaint) => context.DisputeTickets.Add(complaint);
}
