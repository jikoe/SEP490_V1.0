namespace AgriSustain.Application.Interfaces;

/// <summary>An incoming bank credit reported by the payment gateway webhook (SePay).</summary>
/// <param name="TransactionCode">Unique code of the credit at the gateway, e.g. "SEPAY-123".</param>
/// <param name="ReferenceTexts">Transfer content fields that may carry a payment reference.</param>
public sealed record IncomingBankTransfer(
    string Gateway,
    string TransactionCode,
    decimal Amount,
    DateTime? PaidAt,
    string? ProviderReference,
    string RawPayload,
    IReadOnlyList<string?> ReferenceTexts);

/// <summary>
/// Handles bank credits for one payment purpose (B2B subscription, contract payment...). The webhook asks each
/// handler in turn; a handler returns true when the transfer content carries its reference and it has
/// recorded the credit. Unclaimed credits go to the retail order reconciler.
/// </summary>
public interface IBankTransferHandler
{
    Task<bool> TryHandleAsync(IncomingBankTransfer transfer, CancellationToken cancellationToken);
}
