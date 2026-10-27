using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class CartService(ICartRepository repository, IUnitOfWork unitOfWork,
    CartPricingService pricing, TimeProvider clock)
{
    public async Task<CartResponse> GetAsync(long userId, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        var messages = new List<string>();
        foreach (var item in cart?.CartItems ?? [])
        {
            if (item.PreOrderCampaignId is not null || !pricing.IsAvailable(item.Product, item.HarvestBatch))
            {
                item.IsSelected = false;
                messages.Add(CartMessages.Unavailable(item.Product.ProductName));
            }
            else if (item.HarvestBatch is { } batch && item.Quantity > pricing.MaximumQuantity(batch))
            {
                item.Quantity = pricing.MaximumQuantity(batch);
                messages.Add(CartMessages.Adjusted(item.Product.ProductName,
                    batch.AvailableQuantity, item.Quantity, item.Product.Unit));
            }
        }
        if (messages.Count > 0 && cart is not null)
            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return pricing.Calculate(cart, messages);
    }

    public async Task<FeatureResult<CartResponse>> AddItemAsync(long userId,
        CartItemRequest request, CancellationToken cancellationToken)
    {
        if (request.ProductId <= 0 || request.HarvestBatchId <= 0 || request.Quantity <= 0)
        {
            return Fail("VALIDATION", request.Quantity <= 0
                ? CartMessages.InvalidQuantity("đơn vị bán")
                : "Chọn sản phẩm và lô hàng hợp lệ.");
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var batch = await repository.GetOfferAsync(request.ProductId, request.HarvestBatchId,
            cancellationToken);
        if (batch is null || !pricing.IsAvailable(batch.Product, batch))
        {
            return Fail("CONFLICT", CartMessages.Unavailable(batch?.Product.ProductName ?? "Sản phẩm đã chọn"));
        }

        if (!pricing.IsQuantityValid(request.Quantity, batch.Product.Unit))
        {
            return Fail("VALIDATION", CartMessages.InvalidQuantity(batch.Product.Unit));
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
            desired > quantity ? CartMessages.Adjusted(batch.Product.ProductName, batch.AvailableQuantity, quantity, batch.Product.Unit)
                : CartMessages.Added
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
            return Fail("VALIDATION", CartMessages.InvalidQuantity(item.Product.Unit));
        }

        var messages = new List<string>();
        if (!pricing.IsAvailable(item.Product, item.HarvestBatch))
        {
            item.IsSelected = false;
            messages.Add(CartMessages.Unavailable(item.Product.ProductName));
        }
        else if (item.HarvestBatch is { } batch)
        {
            decimal previousPrice = pricing.UnitPrice(item.Product, item.Quantity);
            item.Quantity = decimal.Min(quantity, pricing.MaximumQuantity(batch));
            if (quantity > item.Quantity)
            {
                messages.Add(CartMessages.Adjusted(item.Product.ProductName, batch.AvailableQuantity, item.Quantity, item.Product.Unit));
            }

            decimal currentPrice = pricing.UnitPrice(item.Product, item.Quantity);
            if (previousPrice != currentPrice)
            {
                messages.Add(currentPrice < previousPrice ? CartMessages.WholesaleApplied
                    : CartMessages.RetailRestored);
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
            remove ? CartMessages.Removed(ids.Count) : "Đã cập nhật lựa chọn."
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
                messages.Add(CartMessages.Unavailable(item.Product.ProductName));
            }
            else if (item.HarvestBatch is { } batch
                && item.Quantity > pricing.MaximumQuantity(batch))
            {
                item.Quantity = pricing.MaximumQuantity(batch);
                changed = true;
                messages.Add(CartMessages.Adjusted(item.Product.ProductName, batch.AvailableQuantity, item.Quantity, item.Product.Unit));
            }
        }

        var result = pricing.Calculate(cart);
        if (result.ReviewToken != reviewToken && !changed)
        {
            changed = true;
            messages.Add(CartMessages.PriceChanged);
        }

        if (result.SelectedCount == 0)
        {
            messages.Add(CartMessages.NoSelection);
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

