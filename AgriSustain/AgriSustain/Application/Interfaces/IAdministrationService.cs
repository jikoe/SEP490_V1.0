using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationService
{
    Task<UseCaseResult<Page<UserDto>>> SearchUsersAsync(UserSearch search,
        CancellationToken cancellationToken);
    Task<UseCaseResult<UserDto>> GetUserAsync(long id, CancellationToken cancellationToken);

    Task<UseCaseResult<AccountImpactDto>> GetAccountImpactAsync(long id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes another eligible account after confirmation; audit commits atomically.
    /// </summary>
    Task<UseCaseResult<AccountActionResultDto>> ChangeAccountAsync(long id, AccountActionRequest request,
        CancellationToken cancellationToken);
    Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken);
    Task<UseCaseResult<CategoryDto>> GetCategoryAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Validates BR-CAT-02 and the displayed version before committing category and audit.
    /// </summary>
    Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken);
    /// <summary>
    /// Activate, deactivate or delete (BR-CAT-04): used categories are archived instead of
    /// deleted, and active products block both.
    /// </summary>
    Task<UseCaseResult<CategoryChangeResultDto>> ChangeCategoryAsync(long id, string action,
        ConfirmationRequest request, CancellationToken cancellationToken);
}
