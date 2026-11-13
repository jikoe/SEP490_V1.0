using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationRepository
{
    Task<User?> FindUserAsync(long id, CancellationToken cancellationToken);

    Task<User?> FindActorAsync(long id, CancellationToken cancellationToken);

    /// <summary>Open orders, active contracts and unsettled payouts of the account (MSG-UM-07).</summary>
    Task<AccountImpactDto> GetAccountImpactAsync(long userId, CancellationToken cancellationToken);

    void AddNotification(Notification notification);

    /// <summary>Active staff users with one of the roles, for BR-UM-07 notices.</summary>
    Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    Task<Page<UserDto>> SearchUsersAsync(UserSearch search, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<UserDto?> GetUserAsync(long id, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Category?> FindCategoryAsync(long id, CancellationToken cancellationToken);

    Task<CategorySettings> GetCategorySettingsAsync(long id, CancellationToken cancellationToken);

    Task<Page<CategoryDto>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken);

    Task<CategoryDto?> GetCategoryAsync(long id, CancellationToken cancellationToken);

    Task<bool> CategoryNameExistsAsync(string name, long exceptId, CancellationToken cancellationToken);

    Task<int> CountActiveProductsAsync(long categoryId, CancellationToken cancellationToken);

    Task<bool> HasCategoryReferencesAsync(long categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// BR-CAT-05: archiving a category ends its category-specific fee policies from the archive date;
    /// orders already placed keep their fee lines.
    /// </summary>
    Task EndCategoryFeePoliciesAsync(long categoryId, DateOnly archiveDate, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <summary>Revokes all active refresh tokens when an account is suspended.</summary>
    Task RevokeRefreshTokensForUserAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    void AddCategory(Category category, CategorySettings settings);

    void DeleteCategory(Category category, CategorySettings settings);

    void AddAudit(AuditLog audit);
}
