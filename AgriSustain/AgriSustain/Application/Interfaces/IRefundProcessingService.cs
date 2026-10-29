using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-FIN-03 Process Refund for Finance Staff.</summary>
public interface IRefundProcessingService
{
    Task<Result<RefundListDto>> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken);

    Task<Result<RefundDetailDto>> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken);

    Task<Result<RefundActionResponse>> CompleteAsync(
        long refundId,
        CompleteRefundRequest request,
        CancellationToken cancellationToken);

    Task<Result<RefundActionResponse>> RecordTransferFailureAsync(
        long refundId,
        RefundTransferFailedRequest request,
        CancellationToken cancellationToken);
}
