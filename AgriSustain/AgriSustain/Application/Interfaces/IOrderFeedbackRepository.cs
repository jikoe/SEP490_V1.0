using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IOrderFeedbackRepository
{
    Task<Order?> GetOwnedOrderAsync(long userId, long orderId, CancellationToken cancellationToken);
    Task<Dictionary<long, string>> GetImagesAsync(
        long[] productIds, CancellationToken cancellationToken);
    void Add(ProductReview review);
    void Add(DisputeTicket complaint);
}
