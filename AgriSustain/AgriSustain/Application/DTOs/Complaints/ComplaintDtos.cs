namespace AgriSustain.Application.DTOs.Complaints;

public sealed record ComplaintQuery(
    string? Status,
    string? Reason,
    string? Search,
    int Page = 1,
    int PageSize = 10);

public sealed record ComplaintListItemDto(
    long TicketId,
    string? SellerOrderCode,
    string? OrderCode,
    string ComplainantName,
    string DisputeReason,
    string TicketStatus,
    DateTime CreatedAt,
    decimal? RefundAmount,
    bool HasOverdueEvidence);

public sealed record ComplaintListDto(
    IReadOnlyList<ComplaintListItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<string> Reasons);

public sealed record ComplaintPartyDto(
    long UserId,
    string FullName,
    string? Email,
    string? PhoneNumber);

public sealed record ComplaintEvidenceDto(
    long EvidenceId,
    string SubmittedByName,
    string SubmittedByParty,
    string? Description,
    DateTime SubmittedAt,
    bool IsDocument);

public sealed record ComplaintEvidenceRequestDto(
    long EvidenceRequestId,
    string Party,
    string? Instructions,
    DateTime RequestedAt,
    DateTime? DueAt,
    string RequestStatus,
    DateTime? RespondedAt,
    bool IsOverdue);

public sealed record ComplaintRefundDto(
    string RefundCode,
    decimal Amount,
    string RefundStatus,
    DateTime? ProcessedAt);

/// <summary>A seller-order line; PaidValue is its subtotal less its share of the discount.</summary>
public sealed record ComplaintOrderItemDto(
    long OrderItemId,
    string? ProductName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal PaidValue);

public sealed record ComplaintDetailDto(
    long TicketId,
    string DisputeReason,
    string? Description,
    string TicketStatus,
    string? FaultParty,
    decimal? RefundAmount,
    string? ResolutionNotes,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? AssignedStaffName,
    ComplaintPartyDto Complainant,
    ComplaintPartyDto? Buyer,
    ComplaintPartyDto? Seller,
    long? SellerOrderId,
    string? SellerOrderCode,
    string? SellerOrderStatus,
    string? OrderCode,
    string? OrderStatus,
    decimal? SellerOrderAmount,
    decimal? OrderFinalAmount,
    DateTime? DeliveredAt,
    decimal PaidAmount,
    decimal ReservedRefundAmount,
    decimal SellerOrderReservedRefundAmount,
    decimal MaxRefundAmount,
    IReadOnlyList<ComplaintOrderItemDto> Items,
    IReadOnlyList<ComplaintEvidenceDto> Evidences,
    IReadOnlyList<ComplaintEvidenceRequestDto> EvidenceRequests,
    IReadOnlyList<ComplaintRefundDto> Refunds);

/// <summary>An affected line and quantity for a partial refund (BR-CS-03).</summary>
public sealed record RefundItemRequest(
    long OrderItemId,
    decimal Quantity);

/// <summary>
/// Resolution: FullRefund, PartialRefund or RejectClaim. A partial refund lists the affected
/// items; the system calculates the amount from them.
/// </summary>
public sealed record ResolveComplaintRequest(
    string Resolution,
    string FaultParty,
    IReadOnlyList<RefundItemRequest>? Items,
    string Reason);

/// <summary>Party: Buyer or Seller.</summary>
public sealed record RequestComplaintInfoRequest(
    string Party,
    string Instructions);

public sealed record ComplaintActionResponse(
    long TicketId,
    string TicketStatus,
    string? RefundCode,
    string Message);

public static class ComplaintResolutions
{
    public const string FullRefund = "FullRefund";
    public const string PartialRefund = "PartialRefund";
    public const string RejectClaim = "RejectClaim";
}

public static class ComplaintParties
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string NotDetermined = "NotDetermined";
}
