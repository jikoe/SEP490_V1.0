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
    decimal? RefundAmount);

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
    decimal MaxRefundAmount,
    IReadOnlyList<ComplaintEvidenceDto> Evidences,
    IReadOnlyList<ComplaintEvidenceRequestDto> EvidenceRequests,
    IReadOnlyList<ComplaintRefundDto> Refunds);

/// <summary>Resolution: FullRefund, PartialRefund or RejectClaim.</summary>
public sealed record ResolveComplaintRequest(
    string Resolution,
    string FaultParty,
    decimal? RefundAmount,
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
