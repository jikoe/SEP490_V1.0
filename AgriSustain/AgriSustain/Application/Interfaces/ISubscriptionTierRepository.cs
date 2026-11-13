using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-ADM-09 persistence of B2BSubscriptionPackage. Saving goes through IUseCaseUnitOfWork.</summary>
public interface ISubscriptionTierRepository
{
    /// <summary>
    /// Filtered, paged tier list with subscriber counts and version.
    /// </summary>
    /// <param name="search">Filters and paging.</param>
    /// <param name="now">Current UTC time; a subscription counts while it has not ended.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page.</returns>
    Task<Page<SubscriptionTierDto>> SearchAsync(SubscriptionTierSearch search, DateTime now,
        CancellationToken cancellationToken);

    /// <summary>
    /// One tier read model.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Tier or null.</returns>
    Task<SubscriptionTierDto?> GetAsync(long id, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked tier entity for a change.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Tier or null.</returns>
    Task<B2bsubscriptionPackage?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Names are unique case-insensitively (BR-ST-01); the CI collation decides equality.
    /// </summary>
    /// <param name="name">Trimmed name.</param>
    /// <param name="exceptId">Tier being edited (0 when creating).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when taken.</returns>
    Task<bool> NameExistsAsync(string name, long exceptId, CancellationToken cancellationToken);

    /// <summary>
    /// Current subscribers of the tier (Active and not ended).
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Count.</returns>
    Task<int> CountActiveSubscriptionsAsync(long id, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// True when any subscription, current or past, used the tier (BR-ST-04).
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when purchased at least once.</returns>
    Task<bool> HasSubscriptionsAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new tier.
    /// </summary>
    /// <param name="tier">Tier.</param>
    void Add(B2bsubscriptionPackage tier);

    /// <summary>
    /// Stages the deletion of a tier.
    /// </summary>
    /// <param name="tier">Tier.</param>
    void Remove(B2bsubscriptionPackage tier);

    /// <summary>
    /// Stages an audit row.
    /// </summary>
    /// <param name="audit">Row.</param>
    void AddAudit(AuditLog audit);
}

public interface ISubscriptionTierService
{
    /// <summary>
    /// SD1. Tier list: keyword on the name, status Active / Inactive, sorted by name, paged. Each row shows current
    /// subscribers, whether it was ever purchased and its version token.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN; 400 VALIDATION for invalid filters.</returns>
    Task<UseCaseResult<Page<SubscriptionTierDto>>> SearchAsync(SubscriptionTierSearch search,
        CancellationToken cancellationToken);

    /// <summary>
    /// One tier with subscribers, purchased flag and version.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The tier; 403 / 404.</returns>
    Task<UseCaseResult<SubscriptionTierDto>> GetAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// SD2. Create (id null) or edit a tier. Steps: 1) SubscriptionTierValidator (BR-ST-01); 2) editing needs a version;
    /// 3) transaction + Administrator check; 4) the tier exists and its version still matches (MSG122); 5) changing the
    /// status while editing needs the MSG-ST-03 confirmation; 6) the trimmed name is unique (MSG-ST-02); 7) apply the
    /// values (BR-ST-03: they only affect new purchases and renewals), add when new, save once for the id, audit
    /// CreateTier / UpdateTier with before and after snapshots, save and commit.
    /// </summary>
    /// <param name="id">Tier to edit, or null to create.</param>
    /// <param name="request">Name, description, duration, price, benefits, transaction limit, IsActive, Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-ST-01 "Đã lưu gói …" with the tier; or 400 / 403 / 404 / 409 TIER_CHANGED.</returns>
    Task<UseCaseResult<SubscriptionTierResultDto>> SaveAsync(long? id, SubscriptionTierRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// SD3. Activate or deactivate after confirmation (MSG-ST-03). The version must match and the tier must not already
    /// have that status (MSG122). BR-ST-02: only Active tiers can be bought or renewed; current subscriptions are not
    /// ended. The message tells how many current subscribers keep their purchased terms.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Message with the subscriber count and the tier; or 400 / 403 / 404 / 409.</returns>
    Task<UseCaseResult<SubscriptionTierResultDto>> SetActiveAsync(long id, bool active,
        SubscriptionTierChangeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// SD4. Delete after confirmation (MSG-ST-04). The version must match; a tier ever purchased cannot be deleted
    /// (BR-ST-04, MSG-ST-05 — deactivate it instead). The audit row is written before the removal, in the same
    /// transaction.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>"Đã xóa gói …" without a tier; or 400 / 403 / 404 / 409 TIER_CHANGED / TIER_PURCHASED.</returns>
    Task<UseCaseResult<SubscriptionTierResultDto>> DeleteAsync(long id, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken);
}
