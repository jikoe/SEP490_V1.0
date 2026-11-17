using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;

namespace AgriSustain.Application.Interfaces;

public interface ICustomerComplaintService
{
    Task<Result<PagedResponse<CustomerComplaintListItem>>> ListAsync(
        long buyerId, CustomerComplaintQuery query, CancellationToken ct);

    Task<Result<CustomerComplaintDetail>> GetAsync(long buyerId, long id, CancellationToken ct);
    Task<Result<CustomerComplaintResponse>> RespondAsync(
        long buyerId, long id, CustomerComplaintResponseRequest input, CancellationToken ct);

    Task<string?> GetEvidencePathAsync(long buyerId, long id, long evidenceId, CancellationToken ct);
}
