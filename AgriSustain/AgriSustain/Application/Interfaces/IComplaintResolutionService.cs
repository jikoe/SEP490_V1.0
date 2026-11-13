using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-CS-01 Resolve Complaint for Customer Support / Dispute Officer.</summary>
public interface IComplaintResolutionService
{
    /// <summary>
    /// Lists complaints for the Complaint Resolution screen, undecided ones first.
    /// </summary>
    /// <param name="query">Filters and paging; blank filters mean "all".</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The page, the total count and the distinct reasons for the filter.</returns>
    Task<Result<ComplaintListDto>> SearchAsync(
        ComplaintQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads one complaint and works out the maximum amount that can still be refunded (BR-CS-04).
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or COMPLAINT_NOT_FOUND.</returns>
    Task<Result<ComplaintDetailDto>> GetDetailAsync(
        long ticketId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the Customer Support decision (BR-CS-03) and, for a refund, creates the Finance refund request.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Decision entered on the screen.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The new status and refund code, or the validation / conflict error.</returns>
    Task<Result<ComplaintActionResponse>> ResolveAsync(
        long ticketId,
        ResolveComplaintRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens a 48-hour information request for one party (BR-CS-02) and notifies it in-app and by email.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Target party and instructions.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The complaint in WaitingCustomerInfo, or the validation / conflict error.</returns>
    Task<Result<ComplaintActionResponse>> RequestInformationAsync(
        long ticketId,
        RequestComplaintInfoRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the stored URL of an evidence file that belongs to the given complaint.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="evidenceId">Evidence id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The stored URL, or null when the evidence is not part of that complaint.</returns>
    Task<string?> GetEvidenceFileUrlAsync(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken);
}

/// <summary>UC-CS-01 automatic resolution of complaints whose 48-hour information request expired.</summary>
public interface IComplaintEvidenceTimeoutProcessor
{
    /// <summary>
    /// Decides one batch of complaints whose every open information request has expired (JOB-11).
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>How many complaints were decided in this batch.</returns>
    Task<int> ResolveOverdueAsync(CancellationToken cancellationToken);
}
