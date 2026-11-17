using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "RetailCustomer")]
[Route("api/customer/complaints")]
public sealed class CustomerComplaintsController(
    ICustomerComplaintService service, IFarmerComplaintFiles files, ICurrentActor actor)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CustomerComplaintQuery query, CancellationToken ct) =>
        ToActionResult(await service.ListAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        ToActionResult(await service.GetAsync(actor.UserId, id, ct));

    [HttpPost("{id:long}/responses")]
    public async Task<IActionResult> Submit(
        long id, CustomerComplaintResponseRequest input, CancellationToken ct) =>
        ToActionResult(await service.RespondAsync(actor.UserId, id, input, ct));

    [HttpPost("evidence")]
    [RequestSizeLimit(FarmerComplaintEvidenceRules.MaximumUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = FarmerComplaintEvidenceRules.MaximumVideoBytes)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            return BadRequest(new { code = "MSG183", message = FarmerComplaintMessages.Invalid });
        }
        try
        {
            return Ok(new { fileUrl = await files.SaveResponseEvidenceAsync(actor.UserId, file, ct) });
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { code = "MSG183", message = FarmerComplaintMessages.Invalid });
        }
    }

    [HttpGet("{id:long}/evidence/{evidenceId:long}")]
    public async Task<IActionResult> Evidence(long id, long evidenceId, CancellationToken ct)
    {
        string? path = await service.GetEvidencePathAsync(actor.UserId, id, evidenceId, ct);
        if (path is null)
        {
            return NotFound();
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        string type = OrderFeedbackValidator.EvidenceContentType(path) ?? "application/pdf";
        return PhysicalFile(path, type, enableRangeProcessing: type == "video/mp4");
    }
}
