namespace AgriSustain.Application.DTOs.Orders;

/// <summary>A harvest batch allocated to an order item at checkout, for traceability.</summary>
public sealed record OrderItemBatchDto(
    string BatchCode,
    decimal Quantity,
    DateOnly? HarvestDate,
    string? SeasonName,
    string? TraceabilityUrl);
