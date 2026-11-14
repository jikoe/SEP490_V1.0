using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "WholesaleBuyer")]
[Route("api/b2b")]
public sealed class OfftakeController(IOfftakeService service, IActorContext actor) : ApiControllerBase
{
    [HttpGet("contracts/catalog")]
    public async Task<IActionResult> Catalog([FromQuery] string? keyword, CancellationToken ct) =>
        ToActionResult(await service.CatalogAsync(keyword, ct));

    [HttpPost("contracts")]
    public async Task<IActionResult> Propose([FromBody] OfftakeProposalRequest request,
        CancellationToken ct, [FromQuery] bool draft = false) =>
        ToActionResult(await service.ProposeAsync(request, ct, draft));

    [HttpPost("contracts/drafts")]
    public async Task<IActionResult> SaveDraft([FromBody] OfftakeProposalRequest request,
        CancellationToken ct) => ToActionResult(await service.ProposeAsync(request, ct, draft: true));

    [HttpGet("contracts/{id:long}/draft")]
    public async Task<IActionResult> Draft(long id, CancellationToken ct) =>
        ToActionResult(await service.DraftAsync(id, ct));

    [HttpPut("contracts/{id:long}/draft/{version:int}")]
    public async Task<IActionResult> UpdateDraft(long id, int version,
        [FromBody] OfftakeProposalRequest request, CancellationToken ct,
        [FromQuery] bool submit = false) =>
        ToActionResult(await service.UpdateDraftAsync(id, version, request, submit, ct));

    [HttpPost("contracts/{id:long}/cancellation")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) =>
        ToActionResult(await service.CancelAsync(id, ct));

    [HttpPost("contracts/{id:long}/deposit")]
    public async Task<IActionResult> Deposit(long id, [FromServices] IPaymentQrProvider qrProvider,
        CancellationToken ct) => ToActionResult(await service.RequestDepositAsync(id, qrProvider, ct));

    [HttpGet("contracts/{id:long}/milestones/{milestoneId:long}/payment")]
    public async Task<IActionResult> DeliveryPayment(long id, long milestoneId, [FromServices] IPaymentQrProvider qrProvider,
        CancellationToken ct) => ToActionResult(await service.DeliveryPaymentAsync(id, milestoneId, qrProvider, ct));

    [HttpGet("contracts")]
    public async Task<IActionResult> Contracts([FromQuery] OfftakeTrackingQuery query,
        CancellationToken ct) => ToActionResult(await service.TrackAsync(false, query, ct));

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] OfftakeTrackingQuery query,
        CancellationToken ct) => ToActionResult(await service.TrackAsync(true, query, ct));

    [HttpGet("contracts/{id:long}")]
    public async Task<IActionResult> Contract(long id, CancellationToken ct) =>
        ToActionResult(await service.DetailAsync(false, id, ct));

    [HttpPost("contracts/{id:long}/shipments/{shipmentId:long}/inspection")]
    public async Task<IActionResult> Inspect(long id, long shipmentId, OfftakeInspectionRequest request,
        [FromServices] IFarmerComplaintFiles files, CancellationToken ct) =>
        ToActionResult(await service.InspectAsync(id, shipmentId, request, files, ct));

    [HttpPost("contracts/{id:long}/shipments/{shipmentId:long}/discrepancies/{ticketId:long}/decision")]
    public async Task<IActionResult> DecideDiscrepancy(long id, long shipmentId, long ticketId,
        OfftakeDiscrepancyDecision request, CancellationToken ct) =>
        ToActionResult(await service.DecideDiscrepancyAsync(id, shipmentId, ticketId, request, ct));

    [HttpPost("contracts/{id:long}/evidence")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(long id, IFormFile file,
        [FromServices] IFarmerComplaintFiles files, CancellationToken ct)
    {
        if (!(await service.DetailAsync(false, id, ct)).Success) return NotFound();
        try { return ToActionResult(AgriSustain.Application.Common.Result<object>.Ok(new { url = await files.SaveAsync(actor.UserId, file, ct) })); }
        catch (InvalidDataException) { return BadRequest(new { message = "Tệp bằng chứng không hợp lệ." }); }
    }

    [HttpGet("contracts/{id:long}/evidence/{evidenceId:long}")]
    public async Task<IActionResult> Evidence(long id, long evidenceId, [FromServices] IOfftakeRepository repository,
        [FromServices] IFarmerComplaintFiles files, CancellationToken ct)
    {
        var evidence = await repository.FindContractEvidenceAsync(actor.UserId, id, evidenceId, ct);
        var path = evidence is null ? null : files.Resolve(evidence.SubmittedByUserId, evidence.FileUrl);
        if (path is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, Path.GetExtension(path) switch
        {
            ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/pdf"
        });
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Order(long id, CancellationToken ct) =>
        ToActionResult(await service.DetailAsync(true, id, ct));
}
