using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ICheckoutRepository
{
    /// <summary>
    /// Tracked selected cart lines with product, category, images and farm owner.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Selected lines.</returns>
    Task<IReadOnlyList<CartItem>> SelectedItemsAsync(long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Active delivery addresses of the customer, default first.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Addresses.</returns>
    Task<IReadOnlyList<DeliveryAddress>> AddressesAsync(long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tracked harvest batches of the products, so placement can reserve them.
    /// </summary>
    /// <param name="productIds">Products.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Batches.</returns>
    Task<IReadOnlyList<HarvestBatch>> BatchesAsync(IReadOnlyList<long> productIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Available reward point balance.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Points.</returns>
    Task<long> PointsAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Vouchers that are active, started, not ended and not used up.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Up to 100 vouchers.</returns>
    Task<IReadOnlyList<Voucher>> VouchersAsync(DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Voucher by code.
    /// </summary>
    /// <param name="code">Voucher code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher or null.</returns>
    Task<Voucher?> VoucherAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Uses held by unpaid orders; they count against the usage limit.
    /// </summary>
    /// <param name="voucherId">Voucher.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    Task<int> ReservedVoucherCountAsync(long voucherId, CancellationToken cancellationToken);

    /// <summary>
    /// Uses of each voucher by this customer that still count (reserved or used, not released).
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Voucher id → uses.</returns>
    Task<IReadOnlyDictionary<long, int>> CustomerVoucherUsesAsync(long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stored quote, only when it belongs to the customer.
    /// </summary>
    /// <param name="id">Quote id.</param>
    /// <param name="userId">Customer.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Quote or null.</returns>
    Task<RetailCheckoutQuote?> QuoteAsync(Guid id, long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Order already created by this request key, or else by this quote (idempotency).
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="requestKey">Request key.</param>
    /// <param name="quoteId">Quote id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Order or null.</returns>
    Task<Order?> ExistingOrderAsync(long userId, string requestKey, Guid quoteId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retail order of the customer.
    /// </summary>
    /// <param name="userId">Customer.</param>
    /// <param name="orderId">Order id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Order or null.</returns>
    Task<Order?> GetOrderAsync(long userId, long orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Unpaid checkout orders whose reservation expired, with allocations, vouchers and point entries.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="limit">Batch size.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Tracked orders.</returns>
    Task<IReadOnlyList<Order>> ExpiredOrdersAsync(DateTimeOffset now, int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the category is archived (its products cannot be bought).
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when archived.</returns>
    Task<bool> IsCategoryArchivedAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a quote.
    /// </summary>
    /// <param name="quote">Quote.</param>
    void AddQuote(RetailCheckoutQuote quote);

    /// <summary>
    /// Stages an order with its seller orders and items.
    /// </summary>
    /// <param name="order">Order.</param>
    void AddOrder(Order order);

    /// <summary>
    /// Stages removal of the ordered cart lines.
    /// </summary>
    /// <param name="items">Cart lines.</param>
    void RemoveCartItems(IEnumerable<CartItem> items);

    /// <summary>
    /// Stages an inventory movement.
    /// </summary>
    /// <param name="movement">Movement.</param>
    void AddMovement(InventoryMovement movement);

    /// <summary>
    /// Stages a reward point ledger entry.
    /// </summary>
    /// <param name="entry">Entry.</param>
    void AddRewardEntry(RewardPointEntry entry);
}
