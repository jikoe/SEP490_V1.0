using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFinanceRepository
{
    Task<bool> CanAccessAsync(long userId, CancellationToken cancellationToken);
    Task<decimal> MetricAsync(string metric, FinancePeriod period, CancellationToken ct);
    Task<PagedResponse<FinanceRecord>> RecordsAsync(
        string metric, FinancePeriod period, int page, int pageSize, CancellationToken ct);
    Task<PagedResponse<FinancePaymentRow>> SearchAsync(FinancePaymentQuery query, CancellationToken ct);
    Task<PaymentTransaction?> PaymentAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<AuditLog>> HistoryAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> InvoicesAsync(
        PaymentTransaction payment, CancellationToken ct);
}
