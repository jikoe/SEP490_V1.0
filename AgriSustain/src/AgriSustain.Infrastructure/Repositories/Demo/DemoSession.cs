using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Mappings;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Infrastructure.Data.Demo;

namespace AgriSustain.Infrastructure.Repositories.Demo;

// DEMO-PERSISTENCE: this adapter operates only on the UoW's isolated working snapshot.
// Replace each explicit repository implementation with SQL queries (filter/page in SQL),
// keeping mutation + audit + cart/benefit reservation in the same scoped transaction.
/// <summary>
/// Binds all repositories to one isolated demo snapshot, making mutation, reservation,
/// selected-cart removal and audit share a commit boundary.
/// </summary>
internal sealed class DemoSession(DemoState state) :
    IThreeUcSession, IAccountRepository, ICategoryRepository, ICheckoutRepository
{
    public IAccountRepository Accounts => this;
    public ICategoryRepository Categories => this;
    public ICheckoutRepository Checkout => this;

    /// <summary>
    /// Reads an account or category only from this unit-of-work working copy; missing IDs yield
    /// null and cannot expose another store instance.
    /// </summary>
    Task<UserAccount?> IAccountRepository.FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Accounts.GetValueOrDefault(id));

    /// <summary>
    /// Filters and pages the isolated fixture collection and projects approved response fields.
    /// SQL replacements must perform these operations in SQL rather than load whole tables.
    /// </summary>
    public Task<PagedResponse<UserResponse>> SearchAsync(
        UserQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IEnumerable<UserAccount> users = state.Accounts.Values;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string keyword = query.Keyword.Trim();
            users = users.Where(user => new[] { user.Id.ToString(), user.Name, user.Email,
                user.Phone, user.OrganizationName }.Any(value =>
                    value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));
        }

        users = users.Where(user => (query.Role == null || user.Role == query.Role)
            && (query.Status == null || user.Status == query.Status));
        UserAccount[] filtered = users.OrderBy(user => user.Id).ToArray();
        return Task.FromResult(new PagedResponse<UserResponse>(filtered
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(user => UserMappings.ToResponse(user, now)).ToArray(),
            filtered.Length, query.Page, query.PageSize));
    }

    /// <summary>
    /// Stages a changed account/category in the isolated working copy. Publication to the shared
    /// demo store happens only if the enclosing operation and audit succeed.
    /// </summary>
    public Task SaveAsync(UserAccount account, CancellationToken cancellationToken)
    {
        state.Accounts[account.Id] = account;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads an account or category only from this unit-of-work working copy; missing IDs yield
    /// null and cannot expose another store instance.
    /// </summary>
    Task<Category?> ICategoryRepository.FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Categories.GetValueOrDefault(id));

    /// <summary>
    /// Filters and pages the isolated fixture collection and projects approved response fields.
    /// SQL replacements must perform these operations in SQL rather than load whole tables.
    /// </summary>
    public Task<PagedResponse<CategoryResponse>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken)
    {
        var categories = state.Categories.Values.Where(category =>
            (query.Id == null || category.Id == query.Id)
            && (query.IsActive == null || category.IsActive == query.IsActive)
            && (!query.RootOnly || category.ParentId == null)
            && (query.ParentId == null || category.ParentId == query.ParentId)
            && (string.IsNullOrWhiteSpace(query.Keyword)
                || category.Name.Contains(
                    query.Keyword.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(category => category.Priority).ThenBy(category => category.Id).ToArray();
        return Task.FromResult(new PagedResponse<CategoryResponse>(categories
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(category => new
                CategoryResponse(category.Id, category.Name, category.ParentId,
                    category.ParentId is { } parentId
                        ? state.Categories.GetValueOrDefault(parentId)?.Name : null,
                    category.Description, category.Priority, category.IsActive,
                    state.Products.Values.Count(product => product.CategoryId == category.Id)))
            .ToArray(),
            categories.Length, query.Page, query.PageSize));
    }

    /// <summary>
    /// Checks every fixture offer referencing the category, including unpublished/unapproved
    /// offers; a referenced category can only be hidden.
    /// </summary>
    public Task<bool> HasProductsAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Products.Values.Any(product => product.CategoryId == id));

    /// <summary>
    /// Checks fixture child categories and the mock benchmark/commission reference set. The SQL
    /// adapter must query the real referencing tables and retain their FKs.
    /// </summary>
    public Task<bool> HasOtherReferencesAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Categories.Values.Any(category => category.ParentId == id)
            || state.CategoryPolicyReferences.Contains(id));

    /// <summary>
    /// Removes the category from the isolated working copy. Failure of the later audit prevents
    /// the copy from being committed, restoring the original category.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        state.Categories.Remove(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Advances the working-copy category ID counter only. SQL must use IDENTITY; this is not a
    /// cross-process ID generator.
    /// </summary>
    public Task<long> NextIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(state.NextCategoryId++);

    /// <summary>
    /// Stages a changed account/category in the isolated working copy. Publication to the shared
    /// demo store happens only if the enclosing operation and audit succeed.
    /// </summary>
    public Task SaveAsync(Category category, CancellationToken cancellationToken)
    {
        state.Categories[category.Id] = category;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads buyer-owned cart selections, saved addresses and benefit context. Cart selection is
    /// product-based under DB-05 B; never assign a fixed batch to CartItem.
    /// </summary>
    public Task<CommerceContext> GetContextAsync(
        long buyerId, CancellationToken cancellationToken) =>
        Task.FromResult(new CommerceContext(
            state.Carts.GetValueOrDefault(buyerId, []).ToArray(),
            state.Addresses.GetValueOrDefault(buyerId, []).ToArray(),
            state.Points.GetValueOrDefault(buyerId),
            state.Vouchers.Values.Where(voucher => voucher.IsActive)
                .Select(voucher => voucher.Code).ToArray()));

    /// <summary>
    /// Reads a current offer with category, seller eligibility and pricing; null denotes a
    /// missing product. This read contract does not implement product management.
    /// </summary>
    public Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Products.GetValueOrDefault(id));

    /// <summary>
    /// Reads candidate batches for one product. Application logic filters sellability and
    /// allocates FEFO; SQL should use the approved batch index and avoid one query per
    /// allocation.
    /// </summary>
    public Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HarvestBatch>>(state.Batches.Values
            .Where(batch => batch.ProductId == productId).ToArray());

    /// <summary>
    /// Loads one batch for final reservation revalidation; inventory mutation must remain inside
    /// the shared placement transaction.
    /// </summary>
    public Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Batches.GetValueOrDefault(id));

    /// <summary>
    /// Reads a voucher and current quota projection. Real quota must include valid
    /// VoucherRedemption reservations, not just UsedCount.
    /// </summary>
    public Task<VoucherOffer?> FindVoucherAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(state.Vouchers.Values.FirstOrDefault(voucher =>
            string.Equals(voucher.Code, code, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Loads the saved server quote by identity. The caller must bind it to the current buyer
    /// before exposing or using it.
    /// </summary>
    public Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Quotes.GetValueOrDefault(id));

    /// <summary>
    /// Stages a buyer-bound quote including address, money and FEFO allocation fingerprints.
    /// Preview does not reserve stock; accepted quote retention must support the eventual retry
    /// contract.
    /// </summary>
    public Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken)
    {
        // Bound demo preview history per buyer; orders retain their accepted quote for retries.
        Guid[] removable = state.Quotes.Values.Where(previous => previous.BuyerId == quote.BuyerId
            && !state.Orders.Values.Any(order => order.QuoteId == previous.Id))
            .OrderByDescending(previous => previous.ExpiresAt).Skip(19).Select(q => q.Id).ToArray();
        foreach (Guid id in removable)
        {
            state.Quotes.Remove(id);
        }

        state.Quotes[quote.Id] = quote;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Looks up the buyer-specific DEMO request key. DB-06 is undecided and the current SQL
    /// schema has no request-key column; this contract must be resolved before a durable SQL
    /// adapter is written.
    /// </summary>
    public Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid requestKey, CancellationToken cancellationToken) =>
        Task.FromResult(state.Orders.Values.SingleOrDefault(order =>
            order.BuyerId == buyerId && order.RequestKey == requestKey));

    /// <summary>
    /// Allocates a local order identity; real persistence must use SQL-generated identity values
    /// in the current transaction.
    /// </summary>
    public Task<long> NextOrderIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(state.NextOrderId++);

    /// <summary>
    /// Stages immutable order/seller/item and multi-batch allocation snapshots without
    /// committing. It does not initiate payment or implement order tracking.
    /// </summary>
    public Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        if (state.Orders.Values.Any(existing => existing.BuyerId == order.BuyerId
            && existing.RequestKey == order.RequestKey))
        {
            throw new InvalidOperationException("Duplicate order inside transaction.");
        }

        state.Orders.Add(order.Id, order);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages a batch whose available quantity moved into reserved quantity. SQL must use
    /// conditional updates and check affected rows; callers aggregate demand by batch first.
    /// </summary>
    public Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken)
    {
        state.Batches[batch.Id] = batch;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reserves eligible voucher quota and mock available points for this order. Real points
    /// storage is still undecided; payment/expiry owners must supply idempotent apply/release
    /// behavior.
    /// </summary>
    public Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        // DEMO-BENEFITS: deduct available fixture points and reserve voucher quota here.
        // The rewards/promotion teams must supply durable reservations keyed by OrderId,
        // with apply/release in their payment/expiry flows. No earn/refund UC is implemented.
        int balance = state.Points.GetValueOrDefault(order.BuyerId);
        if (balance < order.RedeemedPoints)
        {
            throw new InvalidOperationException("Points changed inside transaction.");
        }

        state.Points[order.BuyerId] = balance - order.RedeemedPoints;
        if (order.VoucherId is { } voucherId)
        {
            VoucherOffer voucher = state.Vouchers[voucherId];
            state.Vouchers[voucherId] = voucher with
            {
                ReservedCount = voucher.ReservedCount + 1
            };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes only the specified buyer-owned cart rows after placement succeeds. Nonselected
    /// rows remain; rollback must restore any staged removals.
    /// </summary>
    public Task RemoveOrderedItemsAsync(
        long buyerId, IReadOnlyList<long> itemIds, CancellationToken cancellationToken)
    {
        state.Carts[buyerId] = state.Carts.GetValueOrDefault(buyerId, [])
            .Where(item => !itemIds.Contains(item.Id)).ToList();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages the before/after audit beside the business mutation. The fixture is process-local
    /// and is lost on restart, so it is not a durable audit trail.
    /// </summary>
    public Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        state.Audits.Add(record);
        return Task.CompletedTask;
    }
}
