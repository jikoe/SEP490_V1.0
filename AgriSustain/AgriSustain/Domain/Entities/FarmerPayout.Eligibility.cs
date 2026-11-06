namespace AgriSustain.Domain.Entities;

/// <summary>Why a payout cannot be paid right now; Reason is a stable code, Message is shown to Finance.</summary>
public sealed record PayoutBlock(string Reason, string Message);

public partial class FarmerPayout
{
    public const string BlockAccountSuspended = "ACCOUNT_NOT_ACTIVE";
    public const string BlockOpenComplaint = "OPEN_COMPLAINT";
    public const string BlockBankAccountChanged = "BANK_ACCOUNT_RECENTLY_CHANGED";

    /// <summary>
    /// Holds are derived, never stored: each condition is evaluated again when Finance pays, so a
    /// Reactivate (BR-UM-08) lifts only the suspension and the other reasons still apply.
    /// BR-UM-07: the Farmer account must be Active. BR-CS-05: no seller order in the payout may have
    /// an undecided complaint. A bank account changed within the last 48 hours is held as well.
    /// Needs FarmerProfile.User and PayoutItems → OrderItem → SellerOrder → DisputeTickets loaded.
    /// </summary>
    public IReadOnlyList<PayoutBlock> Blockers(UserBankAccount? receivingAccount, DateTime now)
    {
        var blocks = new List<PayoutBlock>();
        if (FarmerProfile.User.Status != "Active")
        {
            blocks.Add(new PayoutBlock(BlockAccountSuspended,
                "Tài khoản nông trại đang bị tạm ngưng hoặc chưa hoạt động; chỉ chi khi tài khoản được kích hoạt lại."));
        }
        var disputedOrders = PayoutItems
            .Select(item => item.OrderItem?.SellerOrder)
            .Where(order => order is not null && order.DisputeTickets.Any(ticket => ticket.IsAwaitingDecision))
            .Select(order => order!.SellerOrderCode)
            .Distinct()
            .ToList();
        if (disputedOrders.Count > 0)
        {
            blocks.Add(new PayoutBlock(BlockOpenComplaint,
                $"Đơn {string.Join(", ", disputedOrders)} đang có khiếu nại chưa được quyết định."));
        }
        if (receivingAccount?.PayoutHoldUntil > now)
        {
            blocks.Add(new PayoutBlock(BlockBankAccountChanged,
                $"Tài khoản nhận tiền vừa được thay đổi; chỉ chi sau {receivingAccount.PayoutHoldUntil:dd/MM/yyyy HH:mm} UTC."));
        }
        return blocks;
    }
}
