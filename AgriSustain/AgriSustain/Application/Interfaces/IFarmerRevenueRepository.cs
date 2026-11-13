using AgriSustain.Application.DTOs.Revenue;

namespace AgriSustain.Application.Interfaces;

public interface IFarmerRevenueRepository
{
    Task<bool> CanReadAsync(long actor, CancellationToken ct);
    Task<IReadOnlyList<FarmerRevenueEvent>> ReadFinancialEventsAsync(long actor, DateTime asOf, CancellationToken ct);
    Task<object> SeasonsAsync(long actor, CancellationToken ct);
}
