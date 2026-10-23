using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces.Services;

public interface ICategoryService
{
    /// <summary>
    /// Validates paging/filter input and reads permitted category rows. Hidden categories remain
    /// queryable by the administrator; there is no archive or image field.
    /// </summary>
    Task<Result<PagedResponse<CategoryResponse>>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken);
    /// <summary>
    /// Validates approved category fields and the parent reference, then saves the category and
    /// audit together. Duplicate-name, cycle-write and Version rules were declined and are not
    /// silently restored.
    /// </summary>
    Task<Result<long>> SaveAsync(
        long? id, SaveCategoryRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Requires explicit confirmation. A delete removes only an unreferenced category; any
    /// product, child, benchmark or commission reference blocks it. Activate/deactivate use
    /// IsActive without archive metadata.
    /// </summary>
    Task<Result<long>> ChangeAsync(
        long id, string action, CategoryActionRequest request, CancellationToken cancellationToken);
}
