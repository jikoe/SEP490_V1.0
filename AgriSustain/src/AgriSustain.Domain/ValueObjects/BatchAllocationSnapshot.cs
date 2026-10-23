namespace AgriSustain.Domain.ValueObjects;

public sealed record BatchAllocationSnapshot(long BatchId, string BatchCode, decimal Quantity);
