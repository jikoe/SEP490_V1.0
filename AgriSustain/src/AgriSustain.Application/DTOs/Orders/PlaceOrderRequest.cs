namespace AgriSustain.Application.DTOs.Orders;

public sealed record PlaceOrderRequest(Guid QuoteId, Guid RequestKey, bool Confirmed);
