using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Infrastructure.Payments;

/// <summary>Reads SePay source debits. Finance authorizes the transfer in its bank app.</summary>
public sealed class SePayRefundGateway(IPayoutGateway reader) : IRejectedOrderRefundGateway
{
    public PayoutTransferPlan? CreateQr(RefundTransaction refund) => reader.CreateQr(new FarmerPayout
    {
        PayoutCode = refund.RefundCode,
        NetAmount = refund.Amount,
        RecipientBankCode = refund.ReceiverBankCode,
        RecipientAccountNumber = refund.ReceiverAccountNumber,
        RecipientAccountHolder = refund.ReceiverAccountHolder
    });

    public async Task<bool> CheckReadAccessAsync(PayoutTransferPlan plan, CancellationToken ct) =>
        (await reader.GetTransferStatusAsync(plan, ct)).LookupAvailable;

    public Task<PayoutGatewayLookup> GetSourceDebitsAsync(PayoutTransferPlan plan, CancellationToken ct) =>
        reader.GetTransferStatusAsync(plan, ct);
}
