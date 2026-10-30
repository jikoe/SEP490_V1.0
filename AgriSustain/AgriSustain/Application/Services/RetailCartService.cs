using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class RetailCartService(ICartRepository repository, IUnitOfWork unitOfWork,
    CartPricingService pricing, TimeProvider clock)
{
    public async Task<CartResponse> GetCartAsync(long userId, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        var messages = pricing.Availability.ReconcileAvailability(cart);
        if (messages.Count > 0 && cart is not null)
            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return pricing.Calculate(cart, messages);
    }

    public async Task<FeatureResult<CartResponse>> AddItemAsync(long userId,
        CartItemRequest request, CancellationToken cancellationToken)
    {
        if (request.ProductId <= 0 || request.HarvestBatchId < 0 || request.Quantity <= 0)
        {
            return Fail("VALIDATION", request.Quantity <= 0
                ? CartMessages.InvalidQuantity("đơn vị bán")
                : "Chọn sản phẩm và lô hàng hợp lệ.");
        }

        if (request.HarvestBatchId == 0)
        {
            return await AddProductAsync(userId, request, cancellationToken);
        }

        await unitOfWork.BeginAsync(cancellationToken);
        var batch = await repository.GetOfferAsync(request.ProductId, request.HarvestBatchId,
            cancellationToken);
        if (batch is null || !pricing.Availability.ValidateLine(batch.Product, batch))
        {
            return Fail("CONFLICT", CartMessages.Unavailable(batch?.Product.ProductName ?? "Sản phẩm đã chọn"));
        }

        if (!pricing.Availability.ValidateQuantity(request.Quantity, batch.Product.Unit))
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
        decimal quantity = decimal.Min(desired, pricing.Availability.MaximumQuantity(batch));
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

        item.ChangeQuantity(quantity);
        cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, new[]
        {
            desired > quantity ? CartMessages.Adjusted(batch.Product.ProductName, batch.AvailableQuantity, quantity, batch.Product.Unit)
                : CartMessages.Added
        }));
    }

    private async Task<FeatureResult<CartResponse>> AddProductAsync(long userId,
        CartItemRequest request, CancellationToken cancellationToken)
    {
        // Catalog quick-add chooses one actual batch; it never merges different batches into one line.
        var batch = (await repository.GetOffersAsync(request.ProductId, cancellationToken))
            .Where(x => pricing.Availability.ValidateLine(x.Product, x))
            .OrderBy(x => !x.ExpirationDate.HasValue).ThenBy(x => x.ExpirationDate)
            .ThenBy(x => x.HarvestBatchId).FirstOrDefault();
        return batch is null ? Fail("CONFLICT", CartMessages.Unavailable("Sản phẩm đã chọn"))
            : await AddItemAsync(userId, request with { HarvestBatchId = batch.HarvestBatchId }, cancellationToken);
    }

    public async Task<FeatureResult<CartResponse>> ChangeQuantityAsync(long userId, long id,
        decimal quantity, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        var cart = await repository.GetOwnedAsync(userId, cancellationToken);
        var item = cart?.CartItems.SingleOrDefault(x => x.CartItemId == id);
        if (cart is null || item is null)
        {
            return Fail("NOT_FOUND", "Không tìm thấy sản phẩm trong giỏ của bạn.");
        }

        if (!pricing.Availability.ValidateQuantity(quantity, item.Product.Unit))
        {
            return Fail("VALIDATION", CartMessages.InvalidQuantity(item.Product.Unit));
        }

        var messages = new List<string>();
        if (!pricing.Availability.ValidateLine(item.Product, item.HarvestBatch))
        {
            item.IsSelected = false;
            messages.Add(CartMessages.Unavailable(item.Product.ProductName));
        }
        else if (item.HarvestBatch is { } batch)
        {
            decimal previousPrice = pricing.Calculate(cart).Items
                .Single(x => x.CartItemId == id).UnitPrice;
            item.ChangeQuantity(pricing.Availability.NormalizeQuantity(quantity, item.Product.Unit, batch.AvailableQuantity));
            if (quantity > item.Quantity)
            {
                messages.Add(CartMessages.Adjusted(item.Product.ProductName, batch.AvailableQuantity, item.Quantity, item.Product.Unit));
            }

            decimal currentPrice = pricing.Calculate(cart).Items
                .Single(x => x.CartItemId == id).UnitPrice;
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
                    && pricing.Availability.ValidateLine(item.Product, item.HarvestBatch);
            }
        }

        var selectionMessages = remove ? new List<string> { CartMessages.Removed(ids.Count) }
            : pricing.Availability.ReconcileAvailability(cart).ToList();
        cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<CartResponse>.Ok(pricing.Calculate(cart, selectionMessages));
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
        var messages = pricing.Availability.ReconcileAvailability(cart, selectedOnly: true).ToList();
        bool changed = messages.Count > 0;

        var result = pricing.Calculate(cart);
        if (!pricing.Availability.CompareSelection(reviewToken, result.ReviewToken) && !changed)
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

    public Task<FeatureResult<CartResponse>> SelectItemsAsync(long userId, CartSelectionRequest input, CancellationToken ct) =>
        SetSelectionAsync(userId, input, false, ct);
    public Task<FeatureResult<CartResponse>> RemoveItemsAsync(long userId, long[] ids, CancellationToken ct) =>
        SetSelectionAsync(userId, new CartSelectionRequest(ids, false), true, ct);

    private static FeatureResult<CartResponse> Fail(string code, string message) =>
        FeatureResult<CartResponse>.Fail(code, message);
}
