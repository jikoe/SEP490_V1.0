using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[Route("api/farmer/farming-logs")]
[ServiceFilter(typeof(FarmInputFilter), Order = int.MinValue)]
public sealed class FarmerFarmingLogController(
    FarmerFarmingLogService service,
    ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Respond(await service.OptionsAsync(actor.UserId, ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] FarmQuery query,
        CancellationToken ct) =>
        Respond(await service.GetLogsAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(
        long id,
        CancellationToken ct) =>
        Respond(await service.GetLogAsync(actor.UserId, id, ct));

    [HttpPost]
    public async Task<IActionResult> Record(
        FarmingLogRequest input,
        CancellationToken ct) =>
        Respond(await service.RecordLogAsync(actor.UserId, input, ct));

    [HttpPost("{originalId:long}/corrections")]
    public async Task<IActionResult> Correct(
        long originalId,
        FarmingLogRequest input,
        CancellationToken ct) =>
        Respond(await service.CorrectLogAsync(actor.UserId, originalId, input, ct));
}
