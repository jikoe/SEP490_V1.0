using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-CS-01 Resolve Complaint for Customer Support / Dispute Officer.</summary>
public interface IComplaintResolutionService
{
    Task<Result<ComplaintListDto>> SearchAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken);

    Task<Result<ComplaintDetailDto>> GetDetailAsync(
        long ticketId,
        CancellationToken cancellationToken);

    Task<Result<ComplaintActionResponse>> ResolveAsync(
        long ticketId,
        ResolveComplaintRequest request,
        CancellationToken cancellationToken);

    Task<Result<ComplaintActionResponse>> RequestInformationAsync(
        long ticketId,
        RequestComplaintInfoRequest request,
        CancellationToken cancellationToken);

    Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken);
}

/// <summary>UC-CS-01 automatic resolution of complaints whose 48-hour information request expired.</summary>
public interface IComplaintEvidenceTimeoutProcessor
{
    /// <summary>Resolves one batch; returns how many complaints were decided.</summary>
    Task<int> ResolveOverdueAsync(CancellationToken cancellationToken);
}
