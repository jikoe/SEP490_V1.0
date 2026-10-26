using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class CartService(ICartRepository repository, IUnitOfWork unitOfWork,
    CartPricingService pricing, TimeProvider clock)
{
    public async Task<CartResponse> GetAsync(long userId, CancellationToken cancellationToken) =>
        pricing.Calculate(await repository.GetOwnedAsync(userId, cancellationToken));

    public async Task<FeatureResult<CartResponse>> AddItemAsync(long userId,
        CartItemRequest request, CancellationToken cancellationToken)
    {
        if (request.ProductId <= 0 || request.HarvestBatchId <= 0 || request.Quantity <= 0)
        {
            return Fail("VALIDATION", "Chọn sản phẩm, lô hàng và số lượng hợp lệ (MSG167).");
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var batch = await repository.GetOfferAsync(request.ProductId, request.HarvestBatchId,
            cancellationToken);
        if (batch is null || !pricing.IsAvailable(batch.Product, batch))
        {
            return Fail("CONFLICT", "Sản phẩm/lô hàng không còn được bán (MSG88).");
        }

        if (!pricing.IsQuantityValid(request.Quantity, batch.Product.Unit))
        {
            return Fail("VALIDATION", "Số lượng sai bước bán hoặc vượt giới hạn (MSG167).");
        }

        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        if (cart is null)
        {
            cart = new Cart { UserId = userId, UpdatedAt = clock.GetUtcNow().UtcDateTime };
            repository.Add(cart);
        }

        var item = cart.CartItems.SingleOrDefault(x => x.ProductId == request.ProductId
            && x.HarvestBatchId == request.HarvestBatchId && x.PreOrderCampaignId == null);
        if (item is null && cart.CartItems.Count >= 100)
        {
            return Fail("VALIDATION", "Giỏ hàng tối đa 100 dòng sản phẩm.");
        }

        decimal desired = (item?.Quantity ?? 0) + request.Quantity;
        decimal quantity = decimal.Min(desired, pricing.MaximumQuantity(batch));
        if (item is null)
        {
            item = new CartItem
            {
                ProductId = request.ProductId, HarvestBatchId = request.HarvestBatchId,
                Product = batch.Product, HarvestBatch = batch,
                IsSelected = true, AddedAt = clock.GetUtcNow().UtcDateTime
            };
            cart.CartItems.Add(item);
        }

        item.Quantity = quantity;
        cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, new[]
        {
            desired > quantity ? $"Đã điều chỉnh còn {quantity} {batch.Product.Unit} (MSG86)."
                : "Đã thêm vào giỏ hàng (MSG81)."
        }));
    }

    public async Task<FeatureResult<CartResponse>> UpdateQuantityAsync(long userId, long id,
        decimal quantity, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        var item = cart?.CartItems.SingleOrDefault(x => x.CartItemId == id);
        if (cart is null || item is null)
        {
            return Fail("NOT_FOUND", "Không tìm thấy sản phẩm trong giỏ của bạn.");
        }

        if (!pricing.IsQuantityValid(quantity, item.Product.Unit))
        {
            return Fail("VALIDATION", "Nhập số lượng dương đúng bước bán (MSG167).");
        }

        var messages = new List<string>();
        if (!pricing.IsAvailable(item.Product, item.HarvestBatch))
        {
            item.IsSelected = false;
            messages.Add("Sản phẩm không còn được bán, đã bỏ chọn (MSG88).");
        }
        else if (item.HarvestBatch is { } batch)
        {
            decimal previousPrice = pricing.UnitPrice(item.Product, item.Quantity);
            item.Quantity = decimal.Min(quantity, pricing.MaximumQuantity(batch));
            if (quantity > item.Quantity)
            {
                messages.Add($"Đã điều chỉnh còn {item.Quantity} {item.Product.Unit} (MSG86).");
            }

            decimal currentPrice = pricing.UnitPrice(item.Product, item.Quantity);
            if (previousPrice != currentPrice)
            {
                messages.Add(currentPrice < previousPrice ? "Áp dụng giá sỉ (MSG82)."
                    : "Áp dụng giá bán lẻ (MSG83).");
            }
        }

        cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, messages));
    }

    public async Task<FeatureResult<CartResponse>> SetSelectionAsync(long userId,
        CartSelectionRequest request, bool remove, CancellationToken cancellationToken)
    {
        if (request.ItemIds is not { Length: > 0 and <= 100 }
            || request.ItemIds.Any(id => id <= 0))
        {
            return Fail("VALIDATION", "Chọn 1–100 dòng sản phẩm hợp lệ.");
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        var ids = request.ItemIds.Distinct().ToHashSet();
        if (cart is null || ids.Any(id => !cart.CartItems.Any(x => x.CartItemId == id)))
        {
            return Fail("NOT_FOUND", "Không tìm thấy dòng trong giỏ hàng của bạn.");
        }

        foreach (var item in cart.CartItems.Where(x => ids.Contains(x.CartItemId)).ToArray())
        {
            if (remove)
            {
                repository.Remove(item);
                cart.CartItems.Remove(item);
            }
            else
            {
                item.IsSelected = request.IsSelected
                    && pricing.IsAvailable(item.Product, item.HarvestBatch);
            }
        }

        cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, new[]
        {
            remove ? $"Đã xóa {ids.Count} sản phẩm (MSG85)." : "Đã cập nhật lựa chọn."
        }));
    }

    public async Task<FeatureResult<CartResponse>> PrepareCheckoutAsync(long userId,
        string reviewToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reviewToken) || reviewToken.Length != 64)
        {
            return Fail("VALIDATION", "Vui lòng tải lại và kiểm tra giỏ hàng.");
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        bool changed = false;
        var messages = new List<string>();
        foreach (var item in cart?.CartItems.Where(x => x.IsSelected) ?? [])
        {
            if (!pricing.IsAvailable(item.Product, item.HarvestBatch))
            {
                item.IsSelected = false;
                changed = true;
                messages.Add($"{item.Product.ProductName} không còn được bán (MSG88).");
            }
            else if (item.HarvestBatch is { } batch
                && item.Quantity > pricing.MaximumQuantity(batch))
            {
                item.Quantity = pricing.MaximumQuantity(batch);
                changed = true;
                messages.Add($"Đã điều chỉnh {item.Product.ProductName}: {item.Quantity} "
                    + $"{item.Product.Unit} (MSG86). Vui lòng kiểm tra lại.");
            }
        }

        var result = pricing.Calculate(cart);
        if (result.ReviewToken != reviewToken && !changed)
        {
            changed = true;
            messages.Add("Giá hoặc tồn kho đã thay đổi. Kiểm tra lại trước khi tiếp tục (MSG118).");
        }

        if (result.SelectedCount == 0)
        {
            messages.Add("Chọn ít nhất một sản phẩm để thanh toán (MSG87).");
        }

        if (changed && cart is not null)
        {
            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, messages,
            !changed && result.SelectedCount > 0));
    }

    private static FeatureResult<CartResponse> Fail(string code, string message) =>
        FeatureResult<CartResponse>.Fail(code, message);
}

