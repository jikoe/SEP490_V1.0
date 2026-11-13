using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-CS-01 Resolve Complaint.</summary>
[ApiController]
[Authorize(Roles = "CustomerSupport,Customer Support,DisputeOfficer")]
[Route("api/support/complaints")]
public sealed class ComplaintsController : ControllerBase
{
    private readonly IComplaintResolutionService _complaintService;
    private readonly IFarmerComplaintFiles _files;

    /// <summary>
    /// Receives the complaint service and the evidence file store from dependency injection.
    /// </summary>
    /// <param name="complaintService">UC-CS-01 application service that holds all business rules.</param>
    /// <param name="files">Resolves a stored evidence URL to a private file on disk.</param>
    public ComplaintsController(
        IComplaintResolutionService complaintService,
        IFarmerComplaintFiles files)
    {
        _complaintService = complaintService;
        _files = files;
    }

    /// <summary>
    /// GET api/support/complaints — screen fields (1)–(9).
    /// Returns one page of complaints filtered by keyword, status and reason, plus the reason list for the filter.
    /// </summary>
    /// <param name="query">Search, Status, Reason, Page and PageSize read from the query string.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with <see cref="ComplaintListDto"/>. The service never fails this call; bad paging values are clamped.</returns>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] ComplaintQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _complaintService.SearchAsync(query, cancellationToken), HttpContext);

    /// <summary>
    /// GET api/support/complaints/{ticketId} — screen fields (10)–(14).
    /// Returns the complaint with its seller order, items, evidence, evidence requests, refunds and the refundable ceiling.
    /// </summary>
    /// <param name="ticketId">Complaint (DisputeTicket) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with <see cref="ComplaintDetailDto"/>, or 404 COMPLAINT_NOT_FOUND.</returns>
    [HttpGet("{ticketId:long}")]
    public async Task<IActionResult> Detail(
        long ticketId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _complaintService.GetDetailAsync(ticketId, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/support/complaints/{ticketId}/resolve — button (19) Confirm decision.
    /// Records Full Refund, Partial Refund or Reject Claim. Nothing is transferred here: an approved refund only
    /// creates a Pending refund request for Finance (UC-FIN-03).
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Resolution, fault party, reason and, for a partial refund, the affected items and quantities.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the new status, refund code and MSG-CS-05 text; 400 for invalid input (MSG-CS-03 / MSG-CS-04);</returns>
    [HttpPost("{ticketId:long}/resolve")]
    public async Task<IActionResult> Resolve(
        long ticketId,
        [FromBody] ResolveComplaintRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _complaintService.ResolveAsync(ticketId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/support/complaints/{ticketId}/request-info — button (20) Request information.
    /// Asks the buyer or the farmer for more evidence and starts that party's 48-hour deadline.
    /// </summary>
    /// <param name="ticketId">Complaint id.</param>
    /// <param name="request">Party (Buyer or Seller) and the instructions shown to that party.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with status WaitingCustomerInfo; 400 for invalid input; 404 when missing;</returns>
    [HttpPost("{ticketId:long}/request-info")]
    public async Task<IActionResult> RequestInformation(
        long ticketId,
        [FromBody] RequestComplaintInfoRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _complaintService.RequestInformationAsync(ticketId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// GET api/support/complaints/{ticketId}/evidence/{evidenceId} — "Xem" link of field (11).
    /// Streams an evidence file. Files live outside wwwroot, so they are only reachable through this authorized
    /// action: the stored URL must have the form /api/.../{owner}/{key}, and <see cref="IFarmerComplaintFiles.Resolve"/>
    /// turns it into a disk path only when it is a file of that owner (no path traversal). Videos are served with
    /// range requests so the browser can seek; responses are not cached.
    /// </summary>
    /// <param name="ticketId">Complaint that must own the evidence.</param>
    /// <param name="evidenceId">Evidence row to open.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The image, PDF or video; 404 when the evidence, its URL or the file does not exist.</returns>
    [HttpGet("{ticketId:long}/evidence/{evidenceId:long}")]
    public async Task<IActionResult> Evidence(
        long ticketId,
        long evidenceId,
        CancellationToken cancellationToken)
    {
        string? fileUrl = await _complaintService.GetEvidenceFileUrlAsync(
            ticketId,
            evidenceId,
            cancellationToken);
        if (fileUrl is null || fileUrl.Split('/') is not { Length: 6 } segments
            || !long.TryParse(segments[4], out long owner))
        {
            return NotFound();
        }

        string? file = _files.Resolve(owner, fileUrl);
        if (file is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        string contentType = OrderFeedbackValidator.EvidenceContentType(file) ?? "application/pdf";
        return PhysicalFile(file, contentType,
            enableRangeProcessing: contentType == OrderFeedbackValidator.VideoContentType);
    }
}
