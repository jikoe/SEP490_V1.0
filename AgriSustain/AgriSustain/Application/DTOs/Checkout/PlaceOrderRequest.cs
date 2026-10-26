namespace AgriSustain.Application.DTOs.Checkout;

public sealed record PlaceOrderRequest(Guid QuoteId, string RequestKey, bool Confirmed);
