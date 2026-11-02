using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;

namespace AgriSustain.Application.Interfaces;

public interface IFinanceService
{
    Task<Result<FinanceDashboardResponse>> DashboardAsync(
        FinancePeriodQuery query, CancellationToken cancellationToken);
    Task<Result<PagedResponse<FinanceRecord>>> RecordsAsync(
        FinanceRecordsQuery query, CancellationToken cancellationToken);
    Task<Result<PagedResponse<FinancePaymentRow>>> SearchAsync(
        FinancePaymentQuery query, CancellationToken cancellationToken);
    Task<Result<FinancePaymentDetail>> DetailAsync(long id, CancellationToken cancellationToken);
    Task<Result<ReconcilePaymentResponse>> ReconcileAsync(
        long id, ReconcilePaymentRequest request, CancellationToken cancellationToken);
}
