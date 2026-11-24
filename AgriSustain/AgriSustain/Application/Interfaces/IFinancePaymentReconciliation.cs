using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IFinancePaymentReconciliation
{
    bool IsConfigured { get; }
    Task<Result<FinanceReconciliationOutcome>> CheckAsync(
        PaymentTransaction payment, long staffId, ReconcilePaymentRequest request, CancellationToken ct);
}

public sealed record FinanceReconciliationOutcome(string Outcome, string Message, bool AlreadyApplied = false);
