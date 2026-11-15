using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IWholesaleOrderRepository
{
    Task<Product?> GetWholesaleProductAsync(long productId, CancellationToken cancellationToken);
    Task<WholesaleBuyerProfile?> GetBuyerKycProfileAsync(long userId, CancellationToken cancellationToken);
    Task<User?> GetUserByIdAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HarvestBatch>> GetAvailableHarvestBatchesAsync(long productId, CancellationToken cancellationToken);
    Task<Order> CreateOrderAsync(Order order, CancellationToken cancellationToken);
    Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken);
    Task<Order?> GetOrderByCodeAsync(string orderCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetBuyerWholesaleOrdersAsync(long buyerUserId, CancellationToken cancellationToken);
    Task<Order?> GetWholesaleOrderForPaymentAsync(string orderCode, CancellationToken cancellationToken);
    Task AddPaymentTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Voucher>> GetActiveVouchersAsync(long farmerProfileId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Voucher>>([]);

    Task<Voucher?> GetVoucherByCodeAsync(string voucherCode, CancellationToken cancellationToken) =>
        Task.FromResult<Voucher?>(null);

    Task<int> GetReservedVoucherCountAsync(long voucherId, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    Task<IReadOnlyDictionary<long, int>> GetCustomerVoucherUsesAsync(long buyerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, int>>(new Dictionary<long, int>());
}

