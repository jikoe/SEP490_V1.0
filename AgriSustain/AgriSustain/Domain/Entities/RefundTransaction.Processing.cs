namespace AgriSustain.Domain.Entities;

/// <summary>
/// Refund request created by UC-CS-01 and settled by Finance in UC-FIN-03 (BR-RF-03):
/// Pending → Processing → Completed (shown as Succeeded) / Failed / NeedsReconciliation.
/// Processing returns to Pending on Cancel Transfer; Failed returns to Processing on Transfer Again;
/// NeedsReconciliation ends as Completed or Failed. "Completed" stays the stored success value because
/// payments, payouts and the finance dashboard already read it.
/// </summary>
public partial class RefundTransaction
{
    public const string StatusPending = "Pending";
    public const string StatusProcessing = "Processing";
    public const string StatusCompleted = "Completed";
    public const string StatusFailed = "Failed";
    public const string StatusNeedsReconciliation = "NeedsReconciliation";

    /// <summary>How Finance paid the refund (database/20261027_refund_method.sql); null until it succeeds.</summary>
    public string? RefundMethod { get; set; }

    /// <summary>Refund states that already consume part of a payment (BR-CS-04: completed and in progress).</summary>
    public bool ReservesPayment => RefundStatus is StatusPending or StatusProcessing or StatusCompleted
        or StatusFailed or StatusNeedsReconciliation or "Success" or "Succeeded";

    /// <summary>
    /// The request waits for Finance to start the transfer.
    /// </summary>
    public bool IsPending => RefundStatus == StatusPending;

    /// <summary>The customer-confirmed receiving account snapshot (BR-RF-05).</summary>
    public bool HasReceivingAccount =>
        !string.IsNullOrWhiteSpace(ReceiverBankCode) && !string.IsNullOrWhiteSpace(ReceiverAccountNumber);

    /// <summary>Start Transfer from Pending, or Transfer Again from Failed, once an account is known.</summary>
    public bool CanStartTransfer => (RefundStatus is StatusPending or StatusFailed) && HasReceivingAccount;

    /// <summary>
    /// UC-FIN-03 AF (Step 3): after a failed transfer, Transfer Again is allowed only once the
    /// RetailCustomer has checked the account again (UC-FIN-08), i.e. confirmed or changed it after
    /// the last recorded failure. lastFailureAt is the time of the latest "RefundTransferFailed" row.
    /// </summary>
    public bool AccountRecheckedSince(DateTime? lastFailureAt) =>
        RefundStatus != StatusFailed || lastFailureAt is null
            || ReceiverAccountProvidedAt is { } provided && provided > lastFailureAt;

    /// <summary>Start Transfer / Transfer Again including the account re-check after a failure.</summary>
    public bool CanStartTransferAfter(DateTime? lastFailureAt) =>
        CanStartTransfer && AccountRecheckedSince(lastFailureAt);

    /// <summary>
    /// UC-FIN-03 AF (Step 3): the customer still has to provide the account (Awaiting customer account)
    /// or check it after a failed transfer, so Finance can remind them (UC-FIN-08).
    /// </summary>
    public bool NeedsAccountFromCustomer(DateTime? lastFailureAt) =>
        RefundStatus is StatusPending or StatusFailed
        && (!HasReceivingAccount || !AccountRecheckedSince(lastFailureAt));

    /// <summary>A transfer was started and its bank result is not recorded yet.</summary>
    public bool AwaitsTransferResult => RefundStatus is StatusProcessing or StatusNeedsReconciliation;

    /// <summary>
    /// Pending or Failed → Processing. Throws when not allowed; the service checks first, so this only guards against bugs.
    /// </summary>
    public void StartTransfer()
    {
        if (!CanStartTransfer)
        {
            throw new InvalidOperationException($"Refund {RefundCode} cannot start a transfer.");
        }

        RefundStatus = StatusProcessing;
    }

    /// <summary>Only before any money was sent: the request goes back to Pending.</summary>
    public void CancelTransfer()
    {
        if (RefundStatus != StatusProcessing)
        {
            throw new InvalidOperationException($"Refund {RefundCode} is not being transferred.");
        }

        RefundStatus = StatusPending;
    }

    /// <summary>
    /// Processing → NeedsReconciliation, when the bank result is unknown.
    /// </summary>
    public void MarkNeedsReconciliation()
    {
        if (RefundStatus != StatusProcessing)
        {
            throw new InvalidOperationException($"Refund {RefundCode} is not being transferred.");
        }

        RefundStatus = StatusNeedsReconciliation;
    }

    /// <summary>
    /// Processing or NeedsReconciliation → Failed.
    /// </summary>
    public void Fail()
    {
        if (!AwaitsTransferResult)
        {
            throw new InvalidOperationException($"Refund {RefundCode} has no transfer to fail.");
        }

        RefundStatus = StatusFailed;
    }

    /// <summary>
    /// Records the confirmed transfer; the payment becomes Refunded once fully covered.
    /// </summary>
    /// <param name="financeUserId">Finance Staff who recorded the result.</param>
    /// <param name="refundMethod">BankTransfer or GatewayRefund.</param>
    /// <param name="transferReference">Bank transaction ID.</param>
    /// <param name="now">Completion time (UTC).</param>
    public void Complete(long financeUserId, string refundMethod, string transferReference, DateTime now)
    {
        if (!AwaitsTransferResult)
        {
            throw new InvalidOperationException($"Refund {RefundCode} has no transfer to complete.");
        }

        RefundStatus = StatusCompleted;
        RefundMethod = refundMethod;
        ProcessedByUserId = financeUserId;
        GatewayReference = transferReference;
        ProcessedAt = now;
        PaymentTransaction.MarkRefundedWhenSettled();
    }

    /// <summary>Account number with only the last four digits visible (BR-RF-07).</summary>
    public static string? Mask(string? accountNumber) =>
        string.IsNullOrEmpty(accountNumber) ? null
            : accountNumber.Length <= 4 ? accountNumber : $"****{accountNumber[^4..]}";
}
