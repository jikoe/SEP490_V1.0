using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-ADM-09 persistence of B2BSubscriptionPackage. Saving goes through IUseCaseUnitOfWork.</summary>
public interface ISubscriptionTierRepository
{
    Task<Page<SubscriptionTierDto>> SearchAsync(SubscriptionTierSearch search, DateTime now,
        CancellationToken cancellationToken);

    Task<SubscriptionTierDto?> GetAsync(long id, DateTime now, CancellationToken cancellationToken);

    Task<B2bsubscriptionPackage?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>Names are unique case-insensitively (BR-ST-01); the CI collation decides equality.</summary>
    Task<bool> NameExistsAsync(string name, long exceptId, CancellationToken cancellationToken);

    Task<int> CountActiveSubscriptionsAsync(long id, DateTime now, CancellationToken cancellationToken);

    /// <summary>True when any subscription, current or past, used the tier (BR-ST-04).</summary>
    Task<bool> HasSubscriptionsAsync(long id, CancellationToken cancellationToken);

    void Add(B2bsubscriptionPackage tier);

    void Remove(B2bsubscriptionPackage tier);

    void AddAudit(AuditLog audit);
}

public interface ISubscriptionTierService
{
    Task<UseCaseResult<Page<SubscriptionTierDto>>> SearchAsync(SubscriptionTierSearch search,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SubscriptionTierDto>> GetAsync(long id, CancellationToken cancellationToken);

    Task<UseCaseResult<SubscriptionTierResultDto>> SaveAsync(long? id, SubscriptionTierRequest request,
        CancellationToken cancellationToken);

    Task<UseCaseResult<SubscriptionTierResultDto>> SetActiveAsync(long id, bool active,
        SubscriptionTierChangeRequest request, CancellationToken cancellationToken);

    Task<UseCaseResult<SubscriptionTierResultDto>> DeleteAsync(long id, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken);
}
