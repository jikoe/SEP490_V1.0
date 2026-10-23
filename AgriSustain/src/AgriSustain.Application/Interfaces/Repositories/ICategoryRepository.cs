using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities.Catalog;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface ICategoryRepository
{
    /// <summary>
    /// Reads one Category by its identity; returns null for missing rows without resurrecting
    /// deleted categories.
    /// </summary>
    Task<Category?> FindAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Returns filtered, stably paged categories with parent names and a count of all product
    /// references, including unpublished/inactive products.
    /// </summary>
    Task<PagedResponse<CategoryResponse>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken);
    /// <summary>
    /// Checks any Product reference, regardless of publication/approval state. A true result
    /// forbids physical deletion under the approved rule.
    /// </summary>
    Task<bool> HasProductsAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Checks child Category, RegionalPriceBenchmark and CommissionPolicy references. SQL must
    /// retain the NO ACTION foreign keys as the final race-condition protection.
    /// </summary>
    Task<bool> HasOtherReferencesAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Allocates an identity inside this session. The demo uses a counter; a SQL implementation
    /// must obtain the database-generated identity rather than MAX(id)+1.
    /// </summary>
    Task<long> NextIdAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Stages approved Category columns only: name, parent, description, priority and IsActive.
    /// Does not introduce image/archive/Version metadata.
    /// </summary>
    Task SaveAsync(Category category, CancellationToken cancellationToken);
    /// <summary>
    /// Stages a physical delete after reference checks. The caller must commit deletion and audit
    /// together; a new competing reference must still be blocked by SQL foreign keys.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
