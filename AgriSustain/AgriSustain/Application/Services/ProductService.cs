using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;

namespace AgriSustain.Application.Services;

public sealed class ProductService(IProductRepository repository, IUnitOfWork unitOfWork,
    IValidator<ProductRequest> validator, IValidator<ProductQuery> queryValidator,
    TimeProvider clock, IFileStorageService storage)
{
    public Task<ProductOptions> OptionsAsync(long userId, CancellationToken cancellationToken) =>
        repository.GetOptionsAsync(userId, cancellationToken);

    public async Task<FeatureResult<PageResult<ProductResponse>>> GetListAsync(long userId,
        ProductQuery query, CancellationToken cancellationToken)
    {
        var validation = await queryValidator.ValidateAsync(query, cancellationToken);
        return validation.IsValid
            ? FeatureResult<PageResult<ProductResponse>>.Ok(
                await repository.SearchAsync(userId, query, cancellationToken))
            : Invalid<PageResult<ProductResponse>>(validation);
    }

    public async Task<FeatureResult<ProductResponse>> GetAsync(long userId, long id,
        CancellationToken cancellationToken)
    {
        var product = await repository.GetOwnedAsync(userId, id, cancellationToken);
        return product is null ? Missing() : FeatureResult<ProductResponse>.Ok(repository.Map(product));
    }

    public async Task<FeatureResult<ProductResponse>> SaveAsync(long userId, long? id,
        ProductRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Invalid<ProductResponse>(validation);
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var farmer = await repository.GetFarmerAsync(userId, cancellationToken);
        if (farmer?.VerificationStatus != "Approved")
        {
            return FeatureResult<ProductResponse>.Fail("FORBIDDEN", ProductListingMessages.MSG75);
        }

        var product = id.HasValue
            ? await repository.GetOwnedAsync(userId, id.Value, cancellationToken) : null;
        if (id.HasValue && product is null)
        {
            return Missing();
        }

        if (!await repository.IsCategoryActiveAsync(request.CategoryId, cancellationToken))
        {
            return FeatureResult<ProductResponse>.Fail("VALIDATION", ProductListingMessages.Field("danh mục", "danh mục đã chọn không hoạt động"));
        }

        if (product is not null && repository.Map(product).Version != request.Version)
        {
            return FeatureResult<ProductResponse>.Fail("CONFLICT", ProductListingMessages.MSG122);
        }

        if (product is not null && product.Unit != request.Unit.Trim()
            && await repository.IsUnitLockedAsync(product.ProductId, cancellationToken))
        {
            return FeatureResult<ProductResponse>.Fail("CONFLICT", ProductListingMessages.MSG79);
        }

        foreach (var image in request.Images)
        {
            bool retained = product?.ProductImages.Any(x => x.ImageUrl == image.ImageUrl) == true;
            if (!retained && !storage.OwnsImage(userId, image.ImageUrl))
            {
                return FeatureResult<ProductResponse>.Fail("VALIDATION",
                    "Ảnh phải được tải lên bằng tài khoản của bạn. " + ProductListingMessages.MSG78);
            }
        }

        var before = product is null ? null : JsonSerializer.Serialize(repository.Map(product));
        bool contentChanged = product is null || product.ApprovalStatus != "Approved"
            || product.ProductName != request.ProductName.Trim()
            || product.CategoryId != request.CategoryId || product.Unit != request.Unit.Trim()
            || product.Description != request.Description.Trim()
            || !repository.Map(product).Images.SequenceEqual(request.Images);
        product ??= new Product
        {
            FarmerProfileId = farmer.FarmerProfileId,
            FarmerProfile = farmer,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        product.ProductName = request.ProductName.Trim();
        product.CategoryId = request.CategoryId;
        product.Unit = request.Unit.Trim();
        product.Description = request.Description.Trim();
        product.BaseRetailPrice = request.BaseRetailPrice;
        product.WholesaleMinQuantity = request.WholesaleMinQuantity;
        product.WholesalePrice = request.WholesalePrice;
        if (contentChanged)
        {
            repository.RemoveImages(product);
            // Remove the previous primary before inserting its replacement (filtered unique index).
            if (id.HasValue) await unitOfWork.FlushAsync(cancellationToken);
            foreach (var image in request.Images)
            {
                product.ProductImages.Add(new ProductImage
                {
                    ImageUrl = image.ImageUrl, IsPrimary = image.IsPrimary,
                    AltText = image.AltText, DisplayOrder = product.ProductImages.Count
                });
            }

            product.SubmitForApproval();
        }

        if (!id.HasValue)
        {
            repository.Add(product);
            await unitOfWork.FlushAsync(cancellationToken);
        }

        repository.Audit(userId, id.HasValue ? "UpdateListing" : "SubmitListing", product.ProductId,
            before, JsonSerializer.Serialize(request));
        await unitOfWork.CommitAsync(cancellationToken);
        return await GetAsync(userId, product.ProductId, cancellationToken);
    }

    public async Task<FeatureResult<ProductResponse>> SetVisibilityAsync(long userId, long id,
        bool published, string version, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        var product = await repository.GetOwnedAsync(userId, id, cancellationToken);
        if (product is null)
        {
            return Missing();
        }

        if (repository.Map(product).Version != version)
        {
            return FeatureResult<ProductResponse>.Fail("CONFLICT", ProductListingMessages.MSG122);
        }

        if (product.FarmerProfile.VerificationStatus != "Approved")
        {
            return FeatureResult<ProductResponse>.Fail("FORBIDDEN", ProductListingMessages.MSG75);
        }

        if (published && product.ApprovalStatus != "Approved")
        {
            return FeatureResult<ProductResponse>.Fail("CONFLICT", "Không thể chuyển sản phẩm sang hiển thị từ trạng thái hiện tại.");
        }

        if (published)
        {
            if (!await repository.IsCategoryActiveAsync(product.CategoryId, cancellationToken))
                return FeatureResult<ProductResponse>.Fail("VALIDATION",
                    ProductListingMessages.Field("danh mục", "danh mục đã chọn không hoạt động"));
            var publication = new ProductRequest(product.ProductName, product.CategoryId,
                product.Unit, product.Description ?? "", product.BaseRetailPrice,
                product.WholesaleMinQuantity, product.WholesalePrice,
                repository.Map(product).Images);
            var validation = await validator.ValidateAsync(publication, cancellationToken);
            if (!validation.IsValid)
                return FeatureResult<ProductResponse>.Fail("VALIDATION",
                    ProductListingMessages.Field("điều kiện hiển thị", "kiểm tra thông tin, giá bán lẻ, cặp giá sỉ và đúng một ảnh chính"));
        }
        product.SetVisibility(published);
        repository.Audit(userId, published ? "PublishListing" : "HideListing", id,
            null, published.ToString());
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<ProductResponse>.Ok(repository.Map(product));
    }

    private static FeatureResult<T> Invalid<T>(ValidationResult validation)
    {
        var result = validation.ToFailure<T>();
        return result with { Error = result.Error! with
        {
            Message = ProductListingMessages.Field("thông tin sản phẩm", "kiểm tra các trường được đánh dấu")
        } };
    }

    private static FeatureResult<ProductResponse> Missing() =>
        FeatureResult<ProductResponse>.Fail("NOT_FOUND", ProductListingMessages.MSG80);
}





