using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ICheckoutRepository
{
    Task<IReadOnlyList<CartItem>> SelectedItemsAsync(long userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryAddress>> AddressesAsync(long userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HarvestBatch>> BatchesAsync(IReadOnlyList<long> productIds,
        CancellationToken cancellationToken);

    Task<long> PointsAsync(long userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Voucher>> VouchersAsync(DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Voucher?> VoucherAsync(string code, CancellationToken cancellationToken);

    Task<int> ReservedVoucherCountAsync(long voucherId, CancellationToken cancellationToken);

    Task<RetailCheckoutQuote?> QuoteAsync(Guid id, long userId,
        CancellationToken cancellationToken);

    Task<Order?> ExistingOrderAsync(long userId, string requestKey, Guid quoteId,
        CancellationToken cancellationToken);

    Task<Order?> GetOrderAsync(long userId, long orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ExpiredOrdersAsync(DateTimeOffset now, int limit,
        CancellationToken cancellationToken);

    Task<bool> IsCategoryArchivedAsync(long id, CancellationToken cancellationToken);

    void AddQuote(RetailCheckoutQuote quote);

    void AddOrder(Order order);

    void RemoveCartItems(IEnumerable<CartItem> items);

    void AddMovement(InventoryMovement movement);

    void AddRewardEntry(RewardPointEntry entry);
}
