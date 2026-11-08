using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "WholesaleBuyer")]
[Route("api/b2b")]
public sealed class OfftakeController(IOfftakeService service) : ApiControllerBase
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

    [HttpPost("contracts/{id:long}/versions/{version:int}/confirmation")]
    public async Task<IActionResult> ConfirmRevision(long id, int version, CancellationToken ct) =>
        ToActionResult(await service.ConfirmRevisionAsync(id, version, ct));

    [HttpGet("contracts")]
    public async Task<IActionResult> Contracts([FromQuery] OfftakeTrackingQuery query,
        CancellationToken ct) => ToActionResult(await service.TrackAsync(false, query, ct));

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] OfftakeTrackingQuery query,
        CancellationToken ct) => ToActionResult(await service.TrackAsync(true, query, ct));

    [HttpGet("contracts/{id:long}")]
    public async Task<IActionResult> Contract(long id, CancellationToken ct) =>
        ToActionResult(await service.DetailAsync(false, id, ct));

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Order(long id, CancellationToken ct) =>
        ToActionResult(await service.DetailAsync(true, id, ct));
}
