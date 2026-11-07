using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-FIN-05 over FeeType, FeePolicy, FeePolicyCategory and OrderItemFee.</summary>
public sealed class FeeRepository(AgriSustainContext db) : IFeeRepository
{
    public async Task<IReadOnlyList<FeeType>> AllTypesAsync(CancellationToken cancellationToken) =>
        await db.FeeTypes.AsNoTracking().Include(type => type.Policies).ThenInclude(policy => policy.Categories)
            .AsSplitQuery().ToListAsync(cancellationToken);

    public Task<FeeType?> FindTypeAsync(long id, CancellationToken cancellationToken) =>
        db.FeeTypes.Include(type => type.Policies).ThenInclude(policy => policy.Categories)
            .AsSplitQuery().SingleOrDefaultAsync(type => type.FeeTypeId == id, cancellationToken);

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

    public Task<FeePolicy?> FindPolicyByRequestKeyAsync(string requestKey, CancellationToken cancellationToken) =>
        db.FeePolicies.AsNoTracking().SingleOrDefaultAsync(policy => policy.RequestKey == requestKey, cancellationToken);

    public Task<bool> CodeExistsAsync(string feeCode, CancellationToken cancellationToken) =>
        db.FeeTypes.AnyAsync(type => type.FeeCode == feeCode, cancellationToken);

    public Task<bool> IsAppliedAsync(long feeTypeId, CancellationToken cancellationToken) =>
        db.OrderItemFees.AnyAsync(fee => fee.FeeTypeId == feeTypeId, cancellationToken);

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

    public async Task<IReadOnlyList<FeePolicy>> ActivePoliciesAsync(string orderType, DateOnly date,
        CancellationToken cancellationToken) =>
        await db.FeePolicies.AsNoTracking().Include(policy => policy.FeeType).Include(policy => policy.Categories)
            .Where(policy => policy.OrderType == orderType && policy.ApprovalStatus == FeePolicy.ApprovalApproved
                && policy.EffectiveFrom <= date && (policy.EffectiveTo == null || policy.EffectiveTo >= date))
            .AsSplitQuery().ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<long, long>> ProductCategoriesAsync(IReadOnlyCollection<long> productIds,
        CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(product => productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, product => product.CategoryId, cancellationToken);

    public async Task<IReadOnlyList<OrderItemFee>> SellerOrderFeesAsync(long sellerOrderId,
        CancellationToken cancellationToken) =>
        await db.OrderItemFees.Where(fee => fee.OrderItem.SellerOrderId == sellerOrderId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<long>> AdministratorIdsAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Status == "Active"
                && (user.Role == "SystemAdmin" || user.Role == "Administrator" || user.Role == "Admin"))
            .Select(user => user.UserId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeeCategoryOption>> SelectableCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking()
            .Where(category => !db.CategorySettings.Any(settings => settings.CategoryId == category.CategoryId
                && settings.IsArchived))
            .OrderBy(category => category.CategoryName)
            .Select(category => new FeeCategoryOption(category.CategoryId, category.CategoryName, category.IsActive))
            .ToListAsync(cancellationToken);

    public void AddType(FeeType feeType) => db.FeeTypes.Add(feeType);

    public void RemoveType(FeeType feeType) => db.FeeTypes.Remove(feeType);

    public void RemoveCategories(IEnumerable<FeePolicyCategory> categories) =>
        db.FeePolicyCategories.RemoveRange(categories);

    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);

    public void AddNotification(Notification notification) => db.Notifications.Add(notification);
}
