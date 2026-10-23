using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Ordering;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface ICheckoutRepository
{
    /// <summary>
    /// Reads buyer-owned cart selections, saved addresses and benefit context. Cart selection is
    /// product-based under DB-05 B; never assign a fixed batch to CartItem.
    /// </summary>
    Task<CommerceContext> GetContextAsync(long buyerId, CancellationToken cancellationToken);
    /// <summary>
    /// Reads a current offer with category, seller eligibility and pricing; null denotes a
    /// missing product. This read contract does not implement product management.
    /// </summary>
    Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Reads candidate batches for one product. Application logic filters sellability and
    /// allocates FEFO; SQL should use the approved batch index and avoid one query per
    /// allocation.
    /// </summary>
    Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken);
    /// <summary>
    /// Loads one batch for final reservation revalidation; inventory mutation must remain inside
    /// the shared placement transaction.
    /// </summary>
    Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Reads a voucher and current quota projection. Real quota must include valid
    /// VoucherRedemption reservations, not just UsedCount.
    /// </summary>
    Task<VoucherOffer?> FindVoucherAsync(string code, CancellationToken cancellationToken);
    /// <summary>
    /// Loads the saved server quote by identity. The caller must bind it to the current buyer
    /// before exposing or using it.
    /// </summary>
    Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>
    /// Stages a buyer-bound quote including address, money and FEFO allocation fingerprints.
    /// Preview does not reserve stock; accepted quote retention must support the eventual retry
    /// contract.
    /// </summary>
    Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken);
    /// <summary>
    /// Looks up the buyer-specific DEMO request key. DB-06 is undecided and the current SQL
    /// schema has no request-key column; this contract must be resolved before a durable SQL
    /// adapter is written.
    /// </summary>
    Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid requestKey, CancellationToken cancellationToken);
    /// <summary>
    /// Allocates a local order identity; real persistence must use SQL-generated identity values
    /// in the current transaction.
    /// </summary>
    Task<long> NextOrderIdAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Stages immutable order/seller/item and multi-batch allocation snapshots without
    /// committing. It does not initiate payment or implement order tracking.
    /// </summary>
    Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken);
    /// <summary>
    /// Stages a batch whose available quantity moved into reserved quantity. SQL must use
    /// conditional updates and check affected rows; callers aggregate demand by batch first.
    /// </summary>
    Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken);
    /// <summary>
    /// Reserves eligible voucher quota and mock available points for this order. Real points
    /// storage is still undecided; payment/expiry owners must supply idempotent apply/release
    /// behavior.
    /// </summary>
    Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken);
    /// <summary>
    /// Removes only the specified buyer-owned cart rows after placement succeeds. Nonselected
    /// rows remain; rollback must restore any staged removals.
    /// </summary>
    Task RemoveOrderedItemsAsync(
        long buyerId, IReadOnlyList<long> itemIds, CancellationToken cancellationToken);
}
