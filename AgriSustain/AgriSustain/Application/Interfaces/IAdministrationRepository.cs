using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationRepository
{
    /// <summary>
    /// Tracked account with its KYC profiles, so the displayed status can be derived and the account changed.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>User or null.</returns>
    Task<User?> FindUserAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Account used for permission checks (also by other use cases).
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>User or null.</returns>
    Task<User?> FindActorAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Open orders, active contracts and unsettled payouts of the account (MSG-UM-07).
    /// </summary>
    /// <param name="userId">Account.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The impact.</returns>
    Task<AccountImpactDto> GetAccountImpactAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a notification row.
    /// </summary>
    /// <param name="notification">Row to insert.</param>
    void AddNotification(Notification notification);

    /// <summary>
    /// Active staff users with one of the roles, for BR-UM-07 notices.
    /// </summary>
    /// <param name="roles">Role names.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>User ids.</returns>
    Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filtered, paged account list with the derived status.
    /// </summary>
    /// <param name="search">Filters and paging.</param>
    /// <param name="actorId">Caller, flagged in each row.</param>
    /// <param name="now">Current time, for the lock flag.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page.</returns>
    Task<Page<UserDto>> SearchUsersAsync(UserSearch search, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// One account with the derived status.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="actorId">Caller.</param>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Account or null.</returns>
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

    /// <summary>
    /// Revokes all active refresh tokens when an account is suspended, so it cannot obtain new access tokens.
    /// </summary>
    /// <param name="userId">Suspended account.</param>
    /// <param name="now">Revocation time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>A task.</returns>
    Task RevokeRefreshTokensForUserAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    void AddCategory(Category category, CategorySettings settings);

    void DeleteCategory(Category category, CategorySettings settings);

    /// <summary>
    /// Stages an audit row.
    /// </summary>
    /// <param name="audit">Row to insert.</param>
    void AddAudit(AuditLog audit);
}
