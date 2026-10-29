using System.Text.RegularExpressions;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-CS-01 Resolve Complaint.</summary>
[ApiController]
[Authorize(Roles = "CustomerSupport,Customer Support,DisputeOfficer")]
[Route("api/support/complaints")]
public sealed partial class ComplaintsController : ControllerBase
{
    private readonly IComplaintResolutionService _complaintService;
    private readonly IWebHostEnvironment _environment;

    public ComplaintsController(
        IComplaintResolutionService complaintService,
        IWebHostEnvironment environment)
    {
        _complaintService = complaintService;
        _environment = environment;
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
        var match = fileUrl is null ? null : EvidenceUrl().Match(fileUrl);
        if (match is null || !match.Success)
        {
            return NotFound();
        }

        string file = Path.Combine(
            _environment.ContentRootPath,
            "private-files",
            match.Groups["owner"].Value,
            match.Groups["key"].Value);
        if (!System.IO.File.Exists(file))
        {
            return NotFound();
        }

        string contentType = Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg"
        };
        return PhysicalFile(file, contentType);
    }

    [GeneratedRegex(
        @"^/api/files/evidence/(?<owner>\d+)/(?<key>[0-9a-f]{32}\.(jpg|jpeg|png|pdf))$",
        RegexOptions.IgnoreCase)]
    private static partial Regex EvidenceUrl();
}
