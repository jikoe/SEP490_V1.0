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

    public ComplaintsController(
        IComplaintResolutionService complaintService,
        IFarmerComplaintFiles files)
    {
        _complaintService = complaintService;
        _files = files;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] ComplaintQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _complaintService.SearchAsync(query, cancellationToken), HttpContext);

    [HttpGet("{ticketId:long}")]
    public async Task<IActionResult> Detail(
        long ticketId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _complaintService.GetDetailAsync(ticketId, cancellationToken), HttpContext);

    [HttpPost("{ticketId:long}/resolve")]
    public async Task<IActionResult> Resolve(
        long ticketId,
        [FromBody] ResolveComplaintRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _complaintService.ResolveAsync(ticketId, request, cancellationToken),
            HttpContext);

    [HttpPost("{ticketId:long}/request-info")]
    public async Task<IActionResult> RequestInformation(
        long ticketId,
        [FromBody] RequestComplaintInfoRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _complaintService.RequestInformationAsync(ticketId, request, cancellationToken),
            HttpContext);

    /// <summary>Lets the reviewer open evidence uploaded by either party of this complaint.</summary>
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
