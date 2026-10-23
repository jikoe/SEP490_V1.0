namespace AgriSustain.Application.DTOs.Orders;

public sealed record ProductOffer(
    long Id, long CategoryId, long SellerId, string SellerName, string Name, string Unit,
    decimal RetailPrice, decimal? WholesalePrice, decimal? WholesaleMinQuantity,
    bool IsApproved = true, bool IsPublished = true, bool IsSellerEligible = true,
    long SellerUserId = 0, string? ImageUrl = null);
