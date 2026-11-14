namespace AgriSustain.Application.Interfaces;

public interface IFarmerPaymentRequestRepository
{
    Task<long?> FarmerIdAsync(long actor, CancellationToken ct);
    Task<IReadOnlyList<long>> FinanceRecipientsAsync(CancellationToken ct);
}
