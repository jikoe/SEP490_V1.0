using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFarmerOrderRepository
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
    Task<UserBankAccount?> LoadPaymentContextAsync(SellerOrder order, CancellationToken ct) =>
        throw new NotSupportedException("Refund destination loading is not implemented.");
    void SaveRefundResultAsync(RefundTransaction refund) =>
        throw new NotSupportedException("Refund persistence is not implemented.");
    void SaveNotificationsAsync(Notification notification) =>
        throw new NotSupportedException("Notification persistence is not implemented.");
    Task<SellerOrder?> LoadOwnedContextAsync(long actorId, long id, CancellationToken ct) => GetOwnedAsync(actorId, id, ct);
    Task SaveFulfillmentAsync(SellerOrder order, IUnitOfWork unitOfWork, CancellationToken ct) => unitOfWork.CommitAsync(ct);
}



