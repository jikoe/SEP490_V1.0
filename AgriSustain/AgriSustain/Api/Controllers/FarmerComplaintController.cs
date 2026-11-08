using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Application.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[Route("api/farmer/complaints")]
[ServiceFilter(typeof(FarmerComplaintInputFilter), Order = int.MinValue)]
public sealed class FarmerComplaintController(
    FarmerComplaintService service,
    IFarmerComplaintFiles files,
    ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FarmerComplaintQuery query, CancellationToken ct) =>
        Respond(await service.GetComplaintsAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Respond(await service.GetComplaintAsync(actor.UserId, id, ct));

    [HttpPost("{id:long}/responses")]
    public async Task<IActionResult> SubmitResponse(long id, FarmerComplaintResponseRequest input, CancellationToken ct) =>
        Respond(await service.SubmitResponseAsync(actor.UserId, id, input, ct));

    [HttpPost("evidence")]
    [RequestSizeLimit(5 * 1024 * 1024 + 65536)]
    public async Task<IActionResult> UploadEvidence(IFormFile? file, CancellationToken ct)
    {
        if (!await service.CanUploadAsync(actor.UserId, ct))
        {
            return StatusCode(403, new { message = FarmerComplaintMessages.Forbidden });
        }
        if (file is null)
        {
            return BadRequest(new { message = FarmerComplaintMessages.Invalid });
        }
        try
        {
            return Ok(new { fileUrl = await files.SaveAsync(actor.UserId, file, ct) });
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = FarmerComplaintMessages.Invalid });
        }
    }

    [HttpGet("{id:long}/evidence/{evidenceId:long}")]
    public async Task<IActionResult> Evidence(long id, long evidenceId, CancellationToken ct)
    {
        string? path = await service.GetEvidenceFileUrlAsync(actor.UserId, id, evidenceId, ct);
        if (path is null)
        {
            return NotFound(new { message = FarmerComplaintMessages.Forbidden });
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        string type = OrderFeedbackValidator.EvidenceContentType(path) ?? "application/pdf";
        return PhysicalFile(path, type,
            enableRangeProcessing: type == OrderFeedbackValidator.VideoContentType);
    }
}

public sealed class FarmerComplaintInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
            context.Result = new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = FarmerComplaintMessages.Invalid });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
