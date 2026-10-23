using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IProductRepository
{
    Task<FarmerProfile?> GetFarmerAsync(long userId, CancellationToken cancellationToken);
    Task<ProductOptions> GetOptionsAsync(long userId, CancellationToken cancellationToken);
    Task<PageResult<ProductResponse>> SearchAsync(long userId, ProductQuery query,
        CancellationToken cancellationToken);
    Task<Product?> GetOwnedAsync(long userId, long id, CancellationToken cancellationToken);
    Task<bool> IsCategoryActiveAsync(long id, CancellationToken cancellationToken);
    Task<bool> IsUnitLockedAsync(long id, CancellationToken cancellationToken);
    void Add(Product product);
    void RemoveImages(Product product);
    void Audit(long userId, string action, long? id, string? before, string after);
    ProductResponse Map(Product product);
}


