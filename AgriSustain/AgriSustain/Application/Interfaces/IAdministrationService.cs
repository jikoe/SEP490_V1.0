using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationService
{
    Task<UseCaseResult<Page<UserDto>>> SearchUsersAsync(UserSearch search,
        CancellationToken cancellationToken);
    Task<UseCaseResult<UserDto>> GetUserAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Changes another eligible account after confirmation; audit commits atomically.
    /// </summary>
    Task<UseCaseResult<UserDto>> ChangeAccountAsync(long id, AccountActionRequest request,
        CancellationToken cancellationToken);
    Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken);
    Task<UseCaseResult<CategoryDto>> GetCategoryAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Validates hierarchy, uniqueness and media before committing category and audit.
    /// </summary>
    Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken);
    /// <summary>
    /// Referenced categories are archived; active product references block removal.
    /// </summary>
    Task<UseCaseResult<bool>> ChangeCategoryAsync(long id, string action, bool confirmed,
        CancellationToken cancellationToken);
}
