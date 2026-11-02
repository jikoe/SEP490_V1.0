using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[Route("api/farmer/harvest-batches")]
[ServiceFilter(typeof(FarmInputFilter), Order = int.MinValue)]
public sealed class FarmerHarvestBatchController(
    FarmerHarvestBatchService service,
    ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Respond(await service.OptionsAsync(actor.UserId, ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] FarmQuery query,
        CancellationToken ct) =>
        Respond(await service.GetBatchesAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(
        long id,
        CancellationToken ct) =>
        Respond(await service.GetBatchAsync(actor.UserId, id, ct));

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Respond(await service.SavePlanAsync(actor.UserId, null, input, ct));

    [HttpPut("{id:long}/plan")]
    public async Task<IActionResult> SavePlan(
        long id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Respond(await service.SavePlanAsync(actor.UserId, id, input, ct));

    [HttpPost("harvest")]
    public async Task<IActionResult> RecordHarvest(
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Respond(await service.RecordHarvestAsync(actor.UserId, null, input, ct));

    [HttpPost("{id:long}/harvest")]
    public async Task<IActionResult> RecordPlannedHarvest(
        long id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Respond(await service.RecordHarvestAsync(actor.UserId, id, input, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateDetails(
        long id,
        HarvestBatchRequest input,
        CancellationToken ct) =>
        Respond(await service.UpdateDetailsAsync(actor.UserId, id, input, ct));

    [HttpPost("{id:long}/issues")]
    public async Task<IActionResult> RecordIssue(
        long id,
        BatchIssueRequest input,
        CancellationToken ct) =>
        Respond(await service.RecordIssueAsync(actor.UserId, id, input, ct));

    [HttpPost("{id:long}/qr")]
    public async Task<IActionResult> GenerateQr(
        long id,
        FarmCommand input,
        CancellationToken ct) =>
        Respond(await service.GenerateQrAsync(actor.UserId, id, input, ct));

    [HttpGet("{id:long}/label")]
    public async Task<IActionResult> Label(
        long id,
        CancellationToken ct) =>
        Respond(await service.GetLabelAsync(actor.UserId, id, ct));
}
