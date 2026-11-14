using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.DTOs.Refunds;

public sealed class RejectedOrderRefundQuery
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public sealed record OpenRejectedOrderRefundRequest(string ExpectedVersion, string RequestKey);
public sealed record RejectedOrderRefundContext(RefundTransaction Refund, SellerOrder Order,
    UserBankAccount? Account, IReadOnlyList<AuditLog> History);
public sealed record RejectedOrderRefundPreview(string ExpectedVersion, PayoutTransferPlan Plan,
    string RecipientName, string Message);
public sealed record RejectedOrderRefundAttempt(PayoutTransferPlan Plan, string RecipientName,
    long FinanceUserId, string RequestKey);
public sealed record RejectedOrderRefundResult(long Id, string Status, string Message);
