namespace AgriSustain.Application.DTOs.Orders;

// UC-RC-01 read model: the buyer selects a product, never a harvest batch (DB-05 B).
public sealed record CartLine(long Id, long ProductId, decimal Quantity, bool Selected);
