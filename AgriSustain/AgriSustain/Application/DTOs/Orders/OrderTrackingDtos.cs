namespace AgriSustain.Application.DTOs.Orders;

public sealed record OrderQuery(
    string? Status,
    string? Keyword,
    int Page = 1,
    int PageSize = 10);

public sealed record OrderSummaryDto(
    long OrderId,
    string OrderCode,
    DateTimeOffset OrderDate,
    string OrderStatus,
    string? PaymentMethod,
    string? DeliveryMethod,
    decimal TotalAmount,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal FinalAmount,
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string? TrackingNumber,
    IReadOnlyList<SellerOrderGroupDto> SellerOrders);

public sealed record SellerOrderGroupDto(
    long SellerOrderId,
    long FarmerProfileId,
    string FarmName,
    string? FarmLocation,
    string SellerOrderCode,
    string Status,
    decimal FinalAmount,
    IReadOnlyList<OrderItemDetailDto> Items);

public sealed record OrderItemDetailDto(
    long OrderItemId,
    long ProductId,
    string ProductName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal SubTotal,
    string? ImageUrl,
    bool IsReviewed,
    IReadOnlyList<OrderItemBatchDto> Batches);

public sealed record OrderDetailDto(
    long OrderId,
    string OrderCode,
    DateTimeOffset OrderDate,
    string OrderStatus,
    string? PaymentMethod,
    string? DeliveryMethod,
    decimal TotalAmount,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal FinalAmount,
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string? TrackingNumber,
    string PaymentStatus,
    DateTimeOffset? ExpectedDeliveryDate,
    IReadOnlyList<SellerOrderGroupDto> SellerOrders,
    IReadOnlyList<OrderStatusTimelineDto> Timeline);

public sealed record OrderStatusTimelineDto(
    string Status,
    DateTimeOffset Timestamp,
    string Description);
