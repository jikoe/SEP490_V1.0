using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IAdministrationRepository
{
    Task<User?> FindUserAsync(long id, CancellationToken cancellationToken);

    Task<User?> FindActorAsync(long id, CancellationToken cancellationToken);

    Task<Page<UserDto>> SearchUsersAsync(UserSearch search, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<UserDto?> GetUserAsync(long id, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Category?> FindCategoryAsync(long id, CancellationToken cancellationToken);

    Task<CategorySettings> GetCategorySettingsAsync(long id, CancellationToken cancellationToken);

    Task<Page<CategoryDto>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken);

    Task<CategoryDto?> GetCategoryAsync(long id, CancellationToken cancellationToken);

    Task<bool> CategoryNameExistsAsync(string name, long? parentId, long exceptId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken cancellationToken);

    Task<bool> HasCategoryReferencesAsync(long categoryId, CancellationToken cancellationToken);

    void AddCategory(Category category, CategorySettings settings);

    void DeleteCategory(Category category, CategorySettings settings);

    void AddAudit(AuditLog audit);
}
