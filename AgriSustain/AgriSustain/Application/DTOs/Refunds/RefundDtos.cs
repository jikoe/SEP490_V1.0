namespace AgriSustain.Application.DTOs.Refunds;

/// <summary>RefundMethod filters completed refunds by how they were paid.</summary>
public sealed record RefundQuery(
    string? Status,
    string? RefundMethod,
    string? Search,
    int Page = 1,
    int PageSize = 10);

public sealed record RefundListItemDto(
    long RefundId,
    string RefundCode,
    decimal Amount,
    string RefundStatus,
    string? RefundMethod,
    long? TicketId,
    string? SellerOrderCode,
    string? OrderCode,
    string? BuyerName,
    DateTime? RequestedAt,
    DateTime? ProcessedAt);

public sealed record RefundListDto(
    IReadOnlyList<RefundListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record RefundAttemptDto(
    string Action,
    string? PerformedByName,
    string? Detail,
    string? Reason,
    DateTime OccurredAt);

public sealed record RefundDetailDto(
    long RefundId,
    string RefundCode,
    decimal Amount,
    string RefundStatus,
    string? Reason,
    string? RefundMethod,
    string? TransferReference,
    DateTime? ProcessedAt,
    string? ProcessedByName,
    long? TicketId,
    string? DisputeReason,
    string? FaultParty,
    string? ResolutionNotes,
    DateTime? DecidedAt,
    string? DecidedByName,
    string? SellerOrderCode,
    string? SellerOrderStatus,
    string? OrderCode,
    string? OrderStatus,
    string? BuyerName,
    string? BuyerEmail,
    string? BuyerPhone,
    string PaymentCode,
    string PaymentProvider,
    string PaymentMethod,
    decimal PaymentAmount,
    string PaymentStatus,
    DateTime? PaidAt,
    IReadOnlyList<RefundAttemptDto> History);

/// <summary>RefundMethod: BankTransfer or GatewayRefund.</summary>
public sealed record CompleteRefundRequest(
    string RefundMethod,
    string TransferReference,
    string? Note);

public sealed record RefundTransferFailedRequest(
    string Reason);

public sealed record RefundActionResponse(
    long RefundId,
    string RefundStatus,
    string Message);

public static class RefundMethods
{
    public const string BankTransfer = "BankTransfer";
    public const string GatewayRefund = "GatewayRefund";
}
