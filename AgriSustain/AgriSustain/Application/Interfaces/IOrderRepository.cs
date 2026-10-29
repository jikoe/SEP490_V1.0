using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IOrderRepository
{
    Task<PageResult<SellerOrderResponse>> SearchOwnedAsync(long userId, SellerOrderQuery query,
        CancellationToken cancellationToken);
    Task<SellerOrder?> GetOwnedAsync(long userId, long id, CancellationToken cancellationToken);
    Task<bool> HasRequestAsync(string requestKey, CancellationToken cancellationToken);
    Task<bool> IsApprovedFarmerAsync(long userId, CancellationToken cancellationToken);
    Task ReleasePurchaseHoldsAsync(SellerOrder order, CancellationToken cancellationToken);
    Task QueueFinanceReviewAsync(SellerOrder order, long actorId,
        CancellationToken cancellationToken);
    Task NotifySupportAsync(long sellerOrderId, string content, CancellationToken cancellationToken);
    void AddMovement(InventoryMovement movement);
    void AddAudit(AuditLog audit);
    void Notify(long userId, string title, string content, long sellerOrderId);
    void AddEvidence(DisputeEvidence evidence);
    SellerOrderResponse Map(SellerOrder order);
}



