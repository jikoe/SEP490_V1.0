namespace AgriSustain.Domain.Entities;

/// <summary>
/// Refund request created by UC-CS-01 and settled by Finance in UC-FIN-03.
/// Pending → Completed; a failed transfer leaves the request Pending (SRS UC-ADM-06 postcondition).
/// </summary>
public partial class RefundTransaction
{
    public const string StatusPending = "Pending";
    public const string StatusCompleted = "Completed";

    /// <summary>How Finance paid the refund (database/20261027_refund_method.sql); null while Pending.</summary>
    public string? RefundMethod { get; set; }

    /// <summary>Refund states that already consume part of a payment.</summary>
    public bool ReservesPayment => RefundStatus is StatusPending or StatusCompleted
        or "Success" or "Succeeded";

    public bool IsPending => RefundStatus == StatusPending;

    /// <summary>Records the confirmed transfer; the payment becomes Refunded once fully covered.</summary>
    public void Complete(long financeUserId, string refundMethod, string transferReference, DateTime now)
    {
        if (!IsPending)
        {
            throw new InvalidOperationException($"Refund {RefundCode} is not pending.");
        }

        RefundStatus = StatusCompleted;
        RefundMethod = refundMethod;
        ProcessedByUserId = financeUserId;
        GatewayReference = transferReference;
        ProcessedAt = now;
        PaymentTransaction.MarkRefundedWhenSettled();
    }
}
