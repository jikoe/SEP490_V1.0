using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IRejectedOrderRefundGateway
{
    PayoutTransferPlan? CreateQr(RefundTransaction refund);
    Task<bool> CheckReadAccessAsync(PayoutTransferPlan plan, CancellationToken ct);
    Task<PayoutGatewayLookup> GetSourceDebitsAsync(PayoutTransferPlan plan, CancellationToken ct);
}
