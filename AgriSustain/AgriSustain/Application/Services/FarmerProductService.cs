using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
namespace AgriSustain.Application.Services;

public sealed class FarmerProductService(IProductRepository repository, IUnitOfWork unitOfWork,
    IValidator<ProductRequest> validator, IValidator<ProductQuery> queryValidator,
    TimeProvider clock, IFileStorageService storage,
    ProductNotificationService? notifications = null, ProductListingRules? listingRules = null)
{
    private readonly ProductListingRules rules = listingRules ?? new();
    public Task<ProductOptions> OptionsAsync(long userId, CancellationToken ct) =>
        repository.GetOptionsAsync(userId, ct);
    public async Task<FeatureResult<PageResult<ProductResponse>>> GetProductsAsync(long userId,
        ProductQuery query, CancellationToken ct)
    {
        var validation = await queryValidator.ValidateAsync(query, ct);
        return validation.IsValid ? FeatureResult<PageResult<ProductResponse>>.Ok(
            await repository.SearchAsync(userId, query, ct)) : Invalid<PageResult<ProductResponse>>(validation);
    }
    public async Task<FeatureResult<ProductResponse>> GetProductAsync(long userId, long id, CancellationToken ct)
    {
        var product = await repository.GetOwnedAsync(userId, id, ct);
        return product is null ? Missing() : FeatureResult<ProductResponse>.Ok(repository.Map(product));
    }
    public async Task<FeatureResult<ProductResponse>> SaveProductAsync(long userId, long? id,
        ProductRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Invalid<ProductResponse>(validation);
        await unitOfWork.BeginAsync(ct);
        var farmer = await repository.GetFarmerAsync(userId, ct);
        if (farmer?.VerificationStatus != "Approved")
            return await Abort("FORBIDDEN", ProductListingMessages.MSG75, ct);
        var product = id.HasValue ? await repository.GetOwnedAsync(userId, id.Value, ct) : null;
        if (id.HasValue && product is null) return await Abort("NOT_FOUND", ProductListingMessages.MSG80, ct);
        if (product is not null && repository.Map(product).Version != request.Version)
            return await Abort("CONFLICT", ProductListingMessages.MSG122, ct);
        if (!await repository.IsCategoryActiveAsync(request.CategoryId, ct))
            return await Abort("VALIDATION", ProductListingMessages.Field("danh mục", "danh mục không hoạt động"), ct);
        if (product is not null && product.Unit != request.Unit.Trim()
            && await repository.IsUnitLockedAsync(product.ProductId, ct))
            return await Abort("CONFLICT", ProductListingMessages.MSG79, ct);
        if (request.PriceOnly && (product is null || !rules.ValidatePriceChange(product, request)))
            return await Abort("VALIDATION", "Nội dung sản phẩm đã thay đổi. Vui lòng chọn Gửi duyệt để kiểm duyệt lại.", ct);
        foreach (var image in request.Images)
            if (product?.ProductImages.Any(x => x.ImageUrl == image.ImageUrl) != true
                && !storage.OwnsImage(userId, image.ImageUrl))
                return await Abort("VALIDATION", ProductListingMessages.MSG78, ct);

        bool newProduct = product is null;
        bool resubmission = product?.ApprovalStatus == "Rejected" && !request.PriceOnly;
        bool contentChanged = product is null || rules.HasContentChanged(product, request);
        bool reviewRequired = newProduct || resubmission || contentChanged;
        var before = product is null ? null : JsonSerializer.Serialize(repository.Map(product));
        product ??= new Product { FarmerProfileId = farmer!.FarmerProfileId,
            FarmerProfile = farmer, CreatedAt = clock.GetUtcNow().UtcDateTime };
        product.ProductName = request.ProductName.Trim(); product.CategoryId = request.CategoryId;
        product.Unit = request.Unit.Trim(); product.Description = request.Description.Trim();
        product.BaseRetailPrice = request.BaseRetailPrice;
        product.WholesaleMinQuantity = request.WholesaleMinQuantity; product.WholesalePrice = request.WholesalePrice;
        if (contentChanged)
        {
            repository.RemoveImages(product);
            if (id.HasValue) await unitOfWork.FlushAsync(ct);
            foreach (var image in request.Images)
                product.ProductImages.Add(new ProductImage { ImageUrl = image.ImageUrl,
                    IsPrimary = image.IsPrimary, AltText = image.AltText, DisplayOrder = product.ProductImages.Count });
        }
        if (reviewRequired) product.SubmitForApproval();
        if (newProduct) { repository.Add(product); await unitOfWork.FlushAsync(ct); }
        bool notificationPending = false;
        if (reviewRequired && notifications is not null)
        {
            await notifications.NotifyModeratorsAsync(product, Guid.NewGuid().ToString("N"), ct);
            notificationPending = true;
        }
        repository.Audit(userId, newProduct ? "SubmitListing" : resubmission ? "ResubmitListing" : "UpdateListing",
            product.ProductId, before, JsonSerializer.Serialize(request));
        await unitOfWork.CommitAsync(ct);
        var response = await GetProductAsync(userId, product.ProductId, ct);
        return response.IsSuccess ? FeatureResult<ProductResponse>.Ok(response.Value! with
        {
            Message = newProduct || resubmission ? ProductListingMessages.MSG71 : ProductListingMessages.MSG72,
            NotificationPending = notificationPending,
            NotificationMessage = notificationPending ? ProductListingMessages.MSG187 : null
        }) : response;
    }
    public async Task<FeatureResult<ProductResponse>> SetVisibilityAsync(long userId, long id,
        bool published, string version, CancellationToken ct)
    {
        await unitOfWork.BeginAsync(ct);
        var product = await repository.GetOwnedAsync(userId, id, ct);
        if (product is null) return await Abort("NOT_FOUND", ProductListingMessages.MSG80, ct);
        if (repository.Map(product).Version != version) return await Abort("CONFLICT", ProductListingMessages.MSG122, ct);
        if (product.FarmerProfile.VerificationStatus != "Approved") return await Abort("FORBIDDEN", ProductListingMessages.MSG75, ct);
        if (!rules.ValidateVisibility(product, published))
            return await Abort("CONFLICT", "Chỉ sản phẩm đã duyệt mới được hiển thị.", ct);
        if (published)
        {
            if (!await repository.IsCategoryActiveAsync(product.CategoryId, ct))
                return await Abort("VALIDATION", ProductListingMessages.Field("danh mục", "danh mục không hoạt động"), ct);
            var input = new ProductRequest(product.ProductName, product.CategoryId, product.Unit,
                product.Description ?? "", product.BaseRetailPrice, product.WholesaleMinQuantity,
                product.WholesalePrice, repository.Map(product).Images);
            var validation = await validator.ValidateAsync(input, ct);
            if (!validation.IsValid) { await unitOfWork.RollbackAsync(ct); return Invalid<ProductResponse>(validation); }
        }
        product.SetVisibility(published);
        repository.Audit(userId, published ? "PublishListing" : "HideListing", id, null, published.ToString());
        await unitOfWork.CommitAsync(ct);
        return FeatureResult<ProductResponse>.Ok(repository.Map(product));
    }
    private async Task<FeatureResult<ProductResponse>> Abort(string code, string message, CancellationToken ct)
    { await unitOfWork.RollbackAsync(ct); return FeatureResult<ProductResponse>.Fail(code, message); }
    private static FeatureResult<ProductResponse> Missing() => FeatureResult<ProductResponse>.Fail("NOT_FOUND", ProductListingMessages.MSG80);
    private static FeatureResult<T> Invalid<T>(ValidationResult validation)
    {
        var result = validation.ToFailure<T>();
        return result with { Error = result.Error! with { Message = ProductListingMessages.Field("thông tin sản phẩm", "kiểm tra các trường được đánh dấu") } };
    }
}
