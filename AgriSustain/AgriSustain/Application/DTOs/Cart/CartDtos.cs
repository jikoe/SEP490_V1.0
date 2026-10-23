namespace AgriSustain.Application.DTOs.Cart;

public sealed record CartItemRequest(long ProductId, long HarvestBatchId, decimal Quantity);
public sealed record QuantityRequest(decimal Quantity);
public sealed record CartSelectionRequest(long[] ItemIds, bool IsSelected);
public sealed record RemoveCartItemsRequest(long[] ItemIds);
public sealed record PrepareCheckoutRequest(string ReviewToken);
public sealed record CartLineResponse(long CartItemId, long ProductId, long? HarvestBatchId,
    string ProductName, string FarmName, long FarmerProfileId, string Unit, string BatchCode,
    string? ImageUrl, decimal Quantity, decimal RetailPrice, decimal UnitPrice,
    decimal? WholesalePrice, decimal? WholesaleMinQuantity, decimal Subtotal,
    decimal AvailableQuantity, decimal QuantityStep, bool IsSelected, bool IsAvailable,
    string? UnavailableReason);
public sealed record CartResponse(IReadOnlyList<CartLineResponse> Items, decimal SelectedSubtotal,
    int SelectedCount, string ReviewToken, bool CanProceedToCheckout,
    IReadOnlyList<string> Messages);

