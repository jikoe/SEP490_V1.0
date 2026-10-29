namespace AgriSustain.Domain.Entities;

/// <summary>How much of a captured payment can still be refunded (UC-CS-01, UC-FIN-03).</summary>
public partial class PaymentTransaction
{
    public const string StatusRefunded = "Refunded";

    public bool IsCaptured => PaymentStatus is "Success" or "Succeeded" or "Paid" or "Completed";

    public decimal ReservedRefundAmount =>
        RefundTransactions.Where(refund => refund.ReservesPayment).Sum(refund => refund.Amount);

    public decimal RefundableAmount => IsCaptured ? decimal.Max(0, Amount - ReservedRefundAmount) : 0;

    /// <summary>Marks the payment Refunded once completed refunds cover the whole amount.</summary>
    public void MarkRefundedWhenSettled()
    {
        decimal completed = RefundTransactions
            .Where(refund => refund.RefundStatus == RefundTransaction.StatusCompleted)
            .Sum(refund => refund.Amount);
        if (IsCaptured && completed >= Amount)
        {
            PaymentStatus = StatusRefunded;
        }
    }
}
