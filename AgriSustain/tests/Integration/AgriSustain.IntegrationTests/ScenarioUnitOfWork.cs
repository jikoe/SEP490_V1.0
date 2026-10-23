using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Ordering;

namespace AgriSustain.IntegrationTests;

/// <summary>
/// Decorates sessions for controlled test failures while delegating to the original unit of
/// work, so test injection preserves the real demo commit/rollback boundary.
/// </summary>
internal sealed class ScenarioUnitOfWork(
    IThreeUcUnitOfWork inner, Func<IThreeUcSession, IThreeUcSession> decorate) : IThreeUcUnitOfWork
{
    /// <summary>
    /// Runs the callback against one consistent session without publishing mutations.
    /// Cancellation is propagated to the operation.
    /// </summary>
    public Task<Result<T>> ReadAsync<T>(Func<IThreeUcSession, Task<Result<T>>> operation,
        CancellationToken cancellationToken) =>
        inner.ReadAsync(session => operation(decorate(session)), cancellationToken);

    /// <summary>
    /// Runs the callback with one shared commit boundary: publish only on a successful Result,
    /// otherwise discard all staged changes, including audit and reservations.
    /// </summary>
    public Task<Result<T>> ExecuteAsync<T>(Func<IThreeUcSession, Task<Result<T>>> operation,
        CancellationToken cancellationToken) =>
        inner.ExecuteAsync(session => operation(decorate(session)), cancellationToken);
}

/// <summary>
/// Wraps one transaction session for failure injection while preserving the same account/category
/// persistence boundary.
/// </summary>
internal sealed class ScenarioSession(
    IThreeUcSession inner, bool failAudit = false, bool changePrice = false) : IThreeUcSession
{
    public IAccountRepository Accounts => inner.Accounts;
    public ICategoryRepository Categories => inner.Categories;
    public ICheckoutRepository Checkout => new ScenarioCheckout(inner.Checkout, changePrice);
    /// <summary>
    /// Optionally throws a controlled audit failure so tests can prove every placement/delete
    /// side effect rolls back; otherwise delegates unchanged.
    /// </summary>
    public Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken) =>
        failAudit ? throw new InvalidOperationException("Injected audit failure.")
            : inner.AddAuditAsync(record, cancellationToken);
}

/// <summary>
/// Decorates the checkout repository for isolated failure/stale-data tests without adding
/// production behavior.
/// </summary>
internal sealed class ScenarioCheckout(ICheckoutRepository inner, bool changePrice)
    : ICheckoutRepository
{
    /// <summary>
    /// Reads buyer-owned cart selections, saved addresses and benefit context. Cart selection is
    /// product-based under DB-05 B; never assign a fixed batch to CartItem.
    /// </summary>
    public Task<CommerceContext> GetContextAsync(
        long buyerId, CancellationToken cancellationToken) =>
        inner.GetContextAsync(buyerId, cancellationToken);
    /// <summary>
    /// Injects a controlled price change into an otherwise unchanged repository read to test
    /// stale-quote rejection. No real product data or SQL database is modified.
    /// </summary>
    public async Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken)
    {
        ProductOffer? product = await inner.FindProductAsync(id, cancellationToken);
        return product != null && changePrice ? product with
        {
            RetailPrice = product.RetailPrice + 10000,
            WholesalePrice = product.WholesalePrice + 10000
        } : product;
    }

    /// <summary>
    /// Reads candidate batches for one product. Application logic filters sellability and
    /// allocates FEFO; SQL should use the approved batch index and avoid one query per
    /// allocation.
    /// </summary>
    public Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken) =>
        inner.FindBatchesAsync(productId, cancellationToken);
    /// <summary>
    /// Loads one batch for final reservation revalidation; inventory mutation must remain inside
    /// the shared placement transaction.
    /// </summary>
    public Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken) =>
        inner.FindBatchAsync(id, cancellationToken);
    /// <summary>
    /// Reads a voucher and current quota projection. Real quota must include valid
    /// VoucherRedemption reservations, not just UsedCount.
    /// </summary>
    public Task<VoucherOffer?> FindVoucherAsync(string code, CancellationToken cancellationToken) =>
        inner.FindVoucherAsync(code, cancellationToken);
    /// <summary>
    /// Loads the saved server quote by identity. The caller must bind it to the current buyer
    /// before exposing or using it.
    /// </summary>
    public Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken) =>
        inner.FindQuoteAsync(id, cancellationToken);
    /// <summary>
    /// Stages a buyer-bound quote including address, money and FEFO allocation fingerprints.
    /// Preview does not reserve stock; accepted quote retention must support the eventual retry
    /// contract.
    /// </summary>
    public Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken) =>
        inner.SaveQuoteAsync(quote, cancellationToken);
    /// <summary>
    /// Looks up the buyer-specific DEMO request key. DB-06 is undecided and the current SQL
    /// schema has no request-key column; this contract must be resolved before a durable SQL
    /// adapter is written.
    /// </summary>
    public Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid key, CancellationToken cancellationToken) =>
        inner.FindOrderAsync(buyerId, key, cancellationToken);
    /// <summary>
    /// Allocates a local order identity; real persistence must use SQL-generated identity values
    /// in the current transaction.
    /// </summary>
    public Task<long> NextOrderIdAsync(CancellationToken cancellationToken) =>
        inner.NextOrderIdAsync(cancellationToken);
    /// <summary>
    /// Stages immutable order/seller/item and multi-batch allocation snapshots without
    /// committing. It does not initiate payment or implement order tracking.
    /// </summary>
    public Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken) =>
        inner.SaveOrderAsync(order, cancellationToken);
    /// <summary>
    /// Stages a batch whose available quantity moved into reserved quantity. SQL must use
    /// conditional updates and check affected rows; callers aggregate demand by batch first.
    /// </summary>
    public Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken) =>
        inner.ReserveBatchAsync(batch, cancellationToken);
    /// <summary>
    /// Reserves eligible voucher quota and mock available points for this order. Real points
    /// storage is still undecided; payment/expiry owners must supply idempotent apply/release
    /// behavior.
    /// </summary>
    public Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken) =>
        inner.ReserveBenefitsAsync(order, cancellationToken);
    /// <summary>
    /// Removes only the specified buyer-owned cart rows after placement succeeds. Nonselected
    /// rows remain; rollback must restore any staged removals.
    /// </summary>
    public Task RemoveOrderedItemsAsync(long buyerId, IReadOnlyList<long> ids,
        CancellationToken cancellationToken) =>
        inner.RemoveOrderedItemsAsync(buyerId, ids, cancellationToken);
}
