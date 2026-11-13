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
    /// <summary>
    /// SubscriptionStatus value of a current subscription.
    /// </summary>
    private const string ActiveSubscription = "Active";

    /// <summary>
    /// Keyword is contained in the name; status maps to IsActive; sorted by name then id; one COUNT and one page query.
    /// The version is added in memory after the query.
    /// </summary>
    /// <param name="search">Filters and paging.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page.</returns>
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

    /// <summary>
    /// One tier with counts and version.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Tier or null.</returns>
    public async Task<SubscriptionTierDto?> GetAsync(long id, DateTime now, CancellationToken cancellationToken)
    {
        var row = await Project(db.B2bsubscriptionPackages.AsNoTracking().Where(tier => tier.PackageId == id), now)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : WithVersion(row);
    }

    /// <summary>
    /// Tracked tier.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Tier or null.</returns>
    public Task<B2bsubscriptionPackage?> FindAsync(long id, CancellationToken cancellationToken) =>
        db.B2bsubscriptionPackages.SingleOrDefaultAsync(tier => tier.PackageId == id, cancellationToken);

    /// <summary>
    /// Another tier with the same name (collation decides case).
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="exceptId">Tier to ignore.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when taken.</returns>
    public Task<bool> NameExistsAsync(string name, long exceptId, CancellationToken cancellationToken) =>
        db.B2bsubscriptionPackages.AnyAsync(tier => tier.PackageId != exceptId && tier.PackageName == name,
            cancellationToken);

    /// <summary>
    /// Active subscriptions of the tier whose end date is in the future.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    public Task<int> CountActiveSubscriptionsAsync(long id, DateTime now, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.CountAsync(subscription => subscription.PackageId == id
            && subscription.SubscriptionStatus == ActiveSubscription && subscription.EndDate > now,
            cancellationToken);

    /// <summary>
    /// Any subscription row of the tier, whatever its status.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when purchased.</returns>
    public Task<bool> HasSubscriptionsAsync(long id, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.AnyAsync(subscription => subscription.PackageId == id, cancellationToken);

    /// <summary>
    /// Adds the tier to the context.
    /// </summary>
    /// <param name="tier">Tier.</param>
    public void Add(B2bsubscriptionPackage tier) => db.B2bsubscriptionPackages.Add(tier);

    /// <summary>
    /// Removes the tier from the context.
    /// </summary>
    /// <param name="tier">Tier.</param>
    public void Remove(B2bsubscriptionPackage tier) => db.B2bsubscriptionPackages.Remove(tier);

    /// <summary>
    /// Adds the audit row to the context.
    /// </summary>
    /// <param name="audit">Row.</param>
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);

    /// <summary>
    /// Maps tiers to the DTO with current subscriber count and purchased flag; the version is filled later.
    /// </summary>
    /// <param name="query">Tiers.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>DTO query.</returns>
    private static IQueryable<SubscriptionTierDto> Project(IQueryable<B2bsubscriptionPackage> query, DateTime now) =>
        query.Select(tier => new SubscriptionTierDto(tier.PackageId, tier.PackageName, tier.PackageDescription,
            tier.DurationDays, tier.Price, tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate,
            tier.TransactionLimit, tier.IsActive,
            tier.B2bsubscriptions.Count(subscription => subscription.SubscriptionStatus == ActiveSubscription
                && subscription.EndDate > now),
            tier.B2bsubscriptions.Any(), ""));

    /// <summary>
    /// Adds the version computed from the displayed values.
    /// </summary>
    /// <param name="tier">Row.</param>
    /// <returns>Row with Version.</returns>
    private static SubscriptionTierDto WithVersion(SubscriptionTierDto tier) => tier with
    {
        Version = B2bsubscriptionPackage.VersionOf(tier.Name, tier.Description, tier.DurationDays, tier.Price,
            tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate, tier.TransactionLimit,
            tier.IsActive)
    };
}
