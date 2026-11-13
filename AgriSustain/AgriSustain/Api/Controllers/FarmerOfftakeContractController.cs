using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[ServiceFilter(typeof(FarmerBusinessInputFilter), Order = int.MinValue)]
[Route("api/farmer/offtake-contracts")]
public sealed class FarmerOfftakeContractController(FarmerOfftakeContractService service,
    ICurrentActor actor, IFarmerComplaintFiles files) : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ContractQuery query, CancellationToken ct) =>
        Respond(await service.GetContractsAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Respond(await service.GetContextAsync(actor.UserId, id, ct));

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> ConfirmTerms(long id, ContractTermsResponseRequest request, CancellationToken ct) =>
        Respond(await service.ConfirmTermsAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> RejectProposal(long id, ContractActionRequest request, CancellationToken ct) =>
        Respond(await service.RejectProposalAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/amend")]
    public async Task<IActionResult> RequestAmendment(long id, AmendContractRequest request, CancellationToken ct) =>
        Respond(await service.RequestAmendmentAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/dispatch")]
    public async Task<IActionResult> DispatchDelivery(long id, DispatchContractDeliveryRequest request, CancellationToken ct) =>
        Respond(await service.DispatchDeliveryAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/delay")]
    public async Task<IActionResult> ReportDelay(long id, ContractDelayRequest request, CancellationToken ct) =>
        Respond(await service.ReportDelayAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/discrepancy")]
    public async Task<IActionResult> SubmitDiscrepancyResponse(long id, ContractDiscrepancyResponseRequest request, CancellationToken ct) =>
        Respond(await service.SubmitDiscrepancyResponseAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/review")]
    public async Task<IActionResult> RequestReview(long id, ContractDiscrepancyResponseRequest request, CancellationToken ct) =>
        Respond(await service.RequestReviewAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/termination")]
    public async Task<IActionResult> RequestTermination(long id, TerminateContractRequest request, CancellationToken ct) =>
        Respond(await service.RequestTerminationAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/progress")]
    public async Task<IActionResult> FulfillmentProgress(long id, CancellationToken ct) =>
        Respond(await service.GetFulfillmentProgressAsync(actor.UserId, id, ct));

    [HttpGet("{id:long}/evidence/{evidenceId:long}")]
    public async Task<IActionResult> Evidence(long id, long evidenceId, CancellationToken ct)
    {
        string? path = await service.GetEvidencePathAsync(actor.UserId, id, evidenceId, ct);
        if (path == null) return NotFound(new { message = FarmerBusinessMessages.Text("MSG121") });
        Response.Headers.CacheControl = "private, no-store";
        string type = Path.GetExtension(path) switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return PhysicalFile(path, type);
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(IFormFile file, CancellationToken ct)
    {
        if (!await service.CanUploadAsync(actor.UserId, ct))
            return StatusCode(403, new ProblemDetails { Title = FarmerBusinessMessages.Text("MSG121") });
        try { return Ok(new { url = await files.SaveAsync(actor.UserId, file, ct) }); }
        catch (InvalidDataException) { return BadRequest(new ProblemDetails { Title = FarmerBusinessMessages.Text("MSG120") }); }
    }
}
