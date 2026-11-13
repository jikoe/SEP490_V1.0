using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationService
{
    /// <summary>
    /// Step 1–2. User list for the Administrator: keyword (id, name, email, phone, farm or company name), role and
    /// account status (BR-UM-01), paged. Each row says whether it is the caller, locked, and may be suspended, so the
    /// screen only shows allowed actions.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Role, Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN for non-administrators; 400 VALIDATION for invalid paging or keyword.</returns>
    Task<UseCaseResult<Page<UserDto>>> SearchUsersAsync(UserSearch search,
        CancellationToken cancellationToken);
    /// <summary>
    /// Step 3–4. One account with its displayed status, KYC status, lock state and failed login count.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The account; 403 / 404.</returns>
    Task<UseCaseResult<UserDto>> GetUserAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Step 6: what a Suspend would affect, for MSG-UM-07 in the confirmation: open orders (as buyer or seller),
    /// active contracts and unsettled payout amount. Read-only.
    /// </summary>
    /// <param name="id">Account to inspect.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts and amount; 403 / 404.</returns>
    Task<UseCaseResult<AccountImpactDto>> GetAccountImpactAsync(long id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes another eligible account after confirmation; audit commits atomically.
    /// </summary>
    /// <param name="id">Account to change.</param>
    /// <param name="request">Action and confirmation data.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The result or the error.</returns>
    Task<UseCaseResult<AccountActionResultDto>> ChangeAccountAsync(long id, AccountActionRequest request,
        CancellationToken cancellationToken);
    /// <summary>
    /// SD1. Category list: keyword on the name, status Active / Inactive / Archived, paged by name. Archived categories
    /// stay listed as read-only history (BR-CAT-01). Each row carries its version token for later actions.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN; 400 VALIDATION for invalid paging, keyword or status.</returns>
    Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken);
    /// <summary>
    /// One category with product counts, archive flag and version token.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The category; 403 / 404.</returns>
    Task<UseCaseResult<CategoryDto>> GetCategoryAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Validates BR-CAT-02 and the displayed version before committing category and audit.
    /// </summary>
    /// <param name="id">Category to edit, or null to create.</param>
    /// <param name="request">Category values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The category or the error.</returns>
    Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken);
    /// <summary>
    /// Activate, deactivate or delete (BR-CAT-04): used categories are archived instead of
    /// deleted, and active products block both.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="action">activate, deactivate or delete.</param>
    /// <param name="request">Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The outcome or the error.</returns>
    Task<UseCaseResult<CategoryChangeResultDto>> ChangeCategoryAsync(long id, string action,
        ConfirmationRequest request, CancellationToken cancellationToken);
}
