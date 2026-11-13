using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-FIN-05 over FeeType, FeePolicy, FeePolicyCategory and OrderItemFee.</summary>
public sealed class FeeRepository(AgriSustainContext db) : IFeeRepository
{
    /// <summary>
    /// All fee types with policies and category links, untracked.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Fee types.</returns>
    public async Task<IReadOnlyList<FeeType>> AllTypesAsync(CancellationToken cancellationToken) =>
        await db.FeeTypes.AsNoTracking().Include(type => type.Policies).ThenInclude(policy => policy.Categories)
            .AsSplitQuery().ToListAsync(cancellationToken);

    /// <summary>
    /// Tracked fee type with policies and category links.
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Fee type or null.</returns>
    public Task<FeeType?> FindTypeAsync(long id, CancellationToken cancellationToken) =>
        db.FeeTypes.Include(type => type.Policies).ThenInclude(policy => policy.Categories)
            .AsSplitQuery().SingleOrDefaultAsync(type => type.FeeTypeId == id, cancellationToken);

    /// <summary>
    /// Tracked policy; its fee type is loaded with all sibling policies so overlap and coverage rules can run.
    /// </summary>
    /// <param name="id">Policy id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Policy or null.</returns>
    public async Task<FeePolicy?> FindPolicyAsync(long id, CancellationToken cancellationToken)
    {
        long? typeId = await db.FeePolicies.Where(policy => policy.FeePolicyId == id)
            .Select(policy => (long?)policy.FeeTypeId).SingleOrDefaultAsync(cancellationToken);
        if (typeId is null)
        {
            return null;
        }
        var type = await FindTypeAsync(typeId.Value, cancellationToken);
        return type?.Policies.SingleOrDefault(policy => policy.FeePolicyId == id);
    }

    /// <summary>
    /// Policy stored with this idempotency key.
    /// </summary>
    /// <param name="requestKey">Key.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Policy or null.</returns>
    public Task<FeePolicy?> FindPolicyByRequestKeyAsync(string requestKey, CancellationToken cancellationToken) =>
        db.FeePolicies.AsNoTracking().SingleOrDefaultAsync(policy => policy.RequestKey == requestKey, cancellationToken);

    /// <summary>
    /// Whether the fee code exists.
    /// </summary>
    /// <param name="feeCode">Fee code.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>True when taken.</returns>
    public Task<bool> CodeExistsAsync(string feeCode, CancellationToken cancellationToken) =>
        db.FeeTypes.AnyAsync(type => type.FeeCode == feeCode, cancellationToken);

    /// <summary>
    /// Whether any order item carries a fee line of this type.
    /// </summary>
    /// <param name="feeTypeId">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>True when applied.</returns>
    public Task<bool> IsAppliedAsync(long feeTypeId, CancellationToken cancellationToken) =>
        db.OrderItemFees.AnyAsync(fee => fee.FeeTypeId == feeTypeId, cancellationToken);

    /// <summary>
    /// Archived flag of the given categories; missing ids are absent from the result.
    /// </summary>
    /// <param name="ids">Category ids.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Category id → archived.</returns>
    public async Task<IReadOnlyDictionary<long, bool>> CategoriesAsync(IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().Where(category => ids.Contains(category.CategoryId))
            .Select(category => new
            {
                category.CategoryId,
                Archived = db.CategorySettings.Any(settings => settings.CategoryId == category.CategoryId
                    && settings.IsArchived)
            })
            .ToDictionaryAsync(row => row.CategoryId, row => row.Archived, cancellationToken);

    /// <summary>
    /// Approved policies of the order type whose period contains the date, with fee type and categories.
    /// </summary>
    /// <param name="orderType">Retail or B2B order type.</param>
    /// <param name="date">Day to evaluate.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Active policies.</returns>
    public async Task<IReadOnlyList<FeePolicy>> ActivePoliciesAsync(string orderType, DateOnly date,
        CancellationToken cancellationToken) =>
        await db.FeePolicies.AsNoTracking().Include(policy => policy.FeeType).Include(policy => policy.Categories)
            .Where(policy => policy.OrderType == orderType && policy.ApprovalStatus == FeePolicy.ApprovalApproved
                && policy.EffectiveFrom <= date && (policy.EffectiveTo == null || policy.EffectiveTo >= date))
            .AsSplitQuery().ToListAsync(cancellationToken);

    /// <summary>
    /// Category of each product.
    /// </summary>
    /// <param name="productIds">Product ids.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Product id → category id.</returns>
    public async Task<IReadOnlyDictionary<long, long>> ProductCategoriesAsync(IReadOnlyCollection<long> productIds,
        CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(product => productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, product => product.CategoryId, cancellationToken);

    /// <summary>
    /// Tracked fee lines of all items of a seller order.
    /// </summary>
    /// <param name="sellerOrderId">Seller order id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Fee lines.</returns>
    public async Task<IReadOnlyList<OrderItemFee>> SellerOrderFeesAsync(long sellerOrderId,
        CancellationToken cancellationToken) =>
        await db.OrderItemFees.Where(fee => fee.OrderItem.SellerOrderId == sellerOrderId).ToListAsync(cancellationToken);

    /// <summary>
    /// Ids of Active administrators.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>User ids.</returns>
    public async Task<IReadOnlyList<long>> AdministratorIdsAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Status == "Active"
                && (user.Role == "SystemAdmin" || user.Role == "Administrator" || user.Role == "Admin"))
            .Select(user => user.UserId).ToListAsync(cancellationToken);

    /// <summary>
    /// Non-archived categories for the policy form.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Options.</returns>
    public async Task<IReadOnlyList<FeeCategoryOption>> SelectableCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .Where(category => !db.CategorySettings.Any(settings => settings.CategoryId == category.CategoryId
                && settings.IsArchived))
            .OrderBy(category => category.CategoryName)
            .Select(category => new FeeCategoryOption(category.CategoryId, category.CategoryName, category.IsActive))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Adds the fee type to the context.
    /// </summary>
    /// <param name="feeType">New fee type.</param>
    public void AddType(FeeType feeType) => db.FeeTypes.Add(feeType);

    /// <summary>
    /// Removes the fee type from the context.
    /// </summary>
    /// <param name="feeType">Fee type to delete.</param>
    public void RemoveType(FeeType feeType) => db.FeeTypes.Remove(feeType);

    /// <summary>
    /// Removes category links from the context.
    /// </summary>
    /// <param name="categories">Links to delete.</param>
    public void RemoveCategories(IEnumerable<FeePolicyCategory> categories) =>
        db.FeePolicyCategories.RemoveRange(categories);

    /// <summary>
    /// Adds the audit row to the context.
    /// </summary>
    /// <param name="audit">New row.</param>
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);

    /// <summary>
    /// Adds the notification row to the context.
    /// </summary>
    /// <param name="notification">New row.</param>
    public void AddNotification(Notification notification) => db.Notifications.Add(notification);
}
