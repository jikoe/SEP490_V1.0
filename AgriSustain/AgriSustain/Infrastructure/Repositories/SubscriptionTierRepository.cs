using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-ADM-09 over dbo.B2BSubscriptionPackage and dbo.B2BSubscription.</summary>
public sealed class SubscriptionTierRepository(AgriSustainContext db) : ISubscriptionTierRepository
{
    private const string ActiveSubscription = "Active";

    public async Task<Page<SubscriptionTierDto>> SearchAsync(SubscriptionTierSearch search, DateTime now,
        CancellationToken cancellationToken)
    {
        var query = db.B2bsubscriptionPackages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            string keyword = search.Keyword.Trim();
            query = query.Where(tier => tier.PackageName.Contains(keyword));
        }
        if (search.Status is "Active" or "Inactive")
        {
            bool active = search.Status == "Active";
            query = query.Where(tier => tier.IsActive == active);
        }
        int total = await query.CountAsync(cancellationToken);
        var rows = await Project(query.OrderBy(tier => tier.PackageName).ThenBy(tier => tier.PackageId)
                .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize), now)
            .ToListAsync(cancellationToken);
        return new Page<SubscriptionTierDto>(rows.Select(WithVersion).ToList(), total, search.Page,
            search.PageSize);
    }

    public async Task<SubscriptionTierDto?> GetAsync(long id, DateTime now, CancellationToken cancellationToken)
    {
        var row = await Project(db.B2bsubscriptionPackages.AsNoTracking().Where(tier => tier.PackageId == id), now)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : WithVersion(row);
    }

    public Task<B2bsubscriptionPackage?> FindAsync(long id, CancellationToken cancellationToken) =>
        db.B2bsubscriptionPackages.SingleOrDefaultAsync(tier => tier.PackageId == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, long exceptId, CancellationToken cancellationToken) =>
        db.B2bsubscriptionPackages.AnyAsync(tier => tier.PackageId != exceptId && tier.PackageName == name,
            cancellationToken);

    public Task<int> CountActiveSubscriptionsAsync(long id, DateTime now, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.CountAsync(subscription => subscription.PackageId == id
            && subscription.SubscriptionStatus == ActiveSubscription && subscription.EndDate > now,
            cancellationToken);

    public Task<bool> HasSubscriptionsAsync(long id, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.AnyAsync(subscription => subscription.PackageId == id, cancellationToken);

    public void Add(B2bsubscriptionPackage tier) => db.B2bsubscriptionPackages.Add(tier);

    public void Remove(B2bsubscriptionPackage tier) => db.B2bsubscriptionPackages.Remove(tier);

    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);

    private static IQueryable<SubscriptionTierDto> Project(IQueryable<B2bsubscriptionPackage> query, DateTime now) =>
        query.Select(tier => new SubscriptionTierDto(tier.PackageId, tier.PackageName, tier.PackageDescription,
            tier.DurationDays, tier.Price, tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate,
            tier.TransactionLimit, tier.IsActive,
            tier.B2bsubscriptions.Count(subscription => subscription.SubscriptionStatus == ActiveSubscription
                && subscription.EndDate > now),
            tier.B2bsubscriptions.Any(), ""));

    private static SubscriptionTierDto WithVersion(SubscriptionTierDto tier) => tier with
    {
        Version = B2bsubscriptionPackage.VersionOf(tier.Name, tier.Description, tier.DurationDays, tier.Price,
            tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate, tier.TransactionLimit,
            tier.IsActive)
    };
}
