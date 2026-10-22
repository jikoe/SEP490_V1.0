namespace AgriSustain.Application.DTOs.Orders;

public sealed record SellerOrderQuery(string? Keyword = null, string? Status = null,
    string? OrderType = null, string? PaymentStatus = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null, int Page = 1, int PageSize = 10);
public sealed record OrderActionRequest(string Version, string RequestKey,
    string? Reason = null, string? Explanation = null, string[]? EvidenceUrls = null,
    long? EvidenceRequestId = null);
public sealed record OrderLineResponse(long OrderItemId, string ProductName, string Unit,
    decimal Quantity, decimal UnitPrice, decimal Subtotal, string? ImageUrl,
    IReadOnlyList<AllocationResponse> Allocations);
public sealed record AllocationResponse(string BatchCode, DateOnly? ExpirationDate,
    decimal AllocatedQuantity, string Status, string BatchStatus);
public sealed record TimelineResponse(string? OldStatus, string NewStatus,
    DateTime OccurredAt, string? Reason);
public sealed record ShipmentResponse(long ShipmentId, string Status, DateTime? ShippedAt,
    DateTime? DeliveredAt, DateTime? ReceivedAt);
public sealed record EvidenceRequestResponse(long EvidenceRequestId, DateTime RequestedAt,
    DateTime? DueAt, string Status, DateTime? RespondedAt, string? Instructions);
public sealed record ComplaintResponse(long DisputeTicketId, string Reason, string? Description,
    string Status, decimal? RefundAmount, string? ResolutionNotes,
    IReadOnlyList<EvidenceRequestResponse> EvidenceRequests);
public sealed record SellerOrderResponse(long SellerOrderId, string SellerOrderCode,
    long OrderId, string OrderCode, string OrderType, DateTime OrderDate, string Status,
    string ParentStatus, string RecipientName, string RecipientPhone, string ShippingAddress,
    string? Notes, decimal Subtotal, decimal DiscountShare, decimal DeliveryFee,
    decimal FinalAmount, string PaymentStatus, decimal VerifiedPayment, decimal RequiredPayment,
    DateTime? DeliveredAt, DateTime? ComplaintDeadline, DateTime? ReceivedAt, string Version,
    bool CanConfirm, bool CanMarkReady, bool CanDispatch, bool CanReject,
    IReadOnlyList<OrderLineResponse> Items, IReadOnlyList<ShipmentResponse> Shipments,
    IReadOnlyList<TimelineResponse> Timeline, IReadOnlyList<ComplaintResponse> Complaints);

