using AgriSustain.Application.DTOs.FarmManagement;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[Route("api/farmer/crop-seasons")]
[ServiceFilter(typeof(FarmInputFilter), Order = int.MinValue)]
public sealed class FarmerCropSeasonController(
    FarmerCropSeasonService service,
    ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Respond(await service.OptionsAsync(actor.UserId, ct));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] FarmQuery query,
        CancellationToken ct) =>
        Respond(await service.GetSeasonsAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(
        long id,
        CancellationToken ct) =>
        Respond(await service.GetSeasonAsync(actor.UserId, id, ct));

    [HttpPost]
    public async Task<IActionResult> CreatePlan(
        SeasonPlanRequest input,
        CancellationToken ct) =>
        Respond(await service.SavePlanAsync(actor.UserId, null, input, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdatePlan(
        long id,
        SeasonPlanRequest input,
        CancellationToken ct) =>
        Respond(await service.SavePlanAsync(actor.UserId, id, input, ct));

    [HttpPost("{id:long}/stage")]
    public async Task<IActionResult> ChangeStage(
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Respond(await service.ChangeStageAsync(actor.UserId, id, input, ct));

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Respond(await service.CompleteSeasonAsync(actor.UserId, id, input, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelOrFail(
        long id,
        SeasonActionRequest input,
        CancellationToken ct) =>
        Respond(await service.CancelSeasonAsync(actor.UserId, id, input, ct));
}
