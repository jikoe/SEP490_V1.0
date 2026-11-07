namespace AgriSustain.Application.DTOs.Refunds;

/// <summary>
/// Status: Pending, Processing, Completed (Succeeded), Failed or NeedsReconciliation.
/// RefundMethod filters completed refunds by how they were paid. FromDate/ToDate are Vietnam dates.
/// </summary>
public sealed record RefundQuery(
    string? Status,
    string? RefundMethod,
    string? Search,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 10);

/// <summary>SourceType: Complaint (UC-CS-01) or FarmerRejection (UC-FAR-06, GBR-34).</summary>
public sealed record RefundListItemDto(
    long RefundId,
    string RefundCode,
    decimal Amount,
    string RefundStatus,
    string? RefundMethod,
    string SourceType,
    long? TicketId,
    string? SellerOrderCode,
    string? OrderCode,
    string? BuyerName,
    bool HasReceivingAccount,
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

/// <summary>
/// ReceiverAccountNumber is shown to Finance only on the transfer view (VietQR); lists and
/// history use ReceiverAccountMasked (BR-RF-07). RefundType: Full, Partial or Cancellation.
/// </summary>
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
    string SourceType,
    string RefundType,
    DateTime? RequestedAt,
    long? TicketId,
    string? DisputeReason,
    string? FaultParty,
    string? ResolutionNotes,
    DateTime? DecidedAt,
    string? DecidedByName,
    string? SellerOrderCode,
    string? SellerOrderStatus,
    decimal? SellerOrderFinalAmount,
    decimal? SellerOrderRefundableAmount,
    string? OrderCode,
    string? OrderStatus,
    string? BuyerName,
    string? BuyerEmail,
    string? BuyerPhone,
    string? ReceiverBankCode,
    string? ReceiverBankName,
    string? ReceiverAccountNumber,
    string? ReceiverAccountMasked,
    string? ReceiverAccountHolder,
    DateTime? ReceiverAccountProvidedAt,
    bool HasReceivingAccount,
    bool CanStartTransfer,
    string PaymentCode,
    string PaymentProvider,
    string PaymentMethod,
    decimal PaymentAmount,
    decimal PaymentRefundableAmount,
    string PaymentStatus,
    DateTime? PaidAt,
    IReadOnlyList<RefundAttemptDto> History,
    bool NeedsCustomerAccount = false,
    DateTime? LastAccountReminderAt = null);

/// <summary>
/// Start Transfer (from Pending) or Transfer Again (from Failed), and Cancel Transfer.
/// ExpectedStatus is the status the screen showed; a different current status is MSG-RF-04.
/// </summary>
public sealed record RefundTransferRequest(
    string? ExpectedStatus);

/// <summary>RefundMethod: BankTransfer or GatewayRefund. TransferReference is the bank transaction ID.</summary>
public sealed record CompleteRefundRequest(
    string RefundMethod,
    string TransferReference,
    string? Note);

public sealed record RefundTransferFailedRequest(
    string Reason);

public sealed record RefundReconciliationRequest(
    string Note);

public sealed record RefundActionResponse(
    long RefundId,
    string RefundStatus,
    string Message);

/// <summary>BR-RF-04 history actions used by UC-FIN-03.</summary>
public static class RefundProcessingActions
{
    public const string TransferFailed = "RefundTransferFailed";
    public const string AccountReminder = "RemindRefundAccount";
}

public static class RefundMethods
{
    public const string BankTransfer = "BankTransfer";
    public const string GatewayRefund = "GatewayRefund";
}

public static class RefundSources
{
    public const string Complaint = "Complaint";
    public const string FarmerRejection = "FarmerRejection";
}
