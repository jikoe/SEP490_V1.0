namespace AgriSustain.Application.DTOs.Orders;

public sealed record VoucherOffer(
    long Id, string Code, long? SellerId, string DiscountType, decimal Value,
    decimal? MaxDiscount, decimal MinOrder, int? UsageLimit, int UsedCount,
    int ReservedCount, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsActive);
