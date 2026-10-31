using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// «boundary» FarmerOrderController
/// UC-FAR-06: Process Order API Boundary
/// Provides endpoints for listing, viewing, confirming, marking ready, simulated shipping,
/// rejecting (with simulated refund and customer email), and generating packing slips.
/// </summary>
[Authorize(Roles = "Farmer")]
[Route("api/farmer/orders")]
public sealed class FarmerOrderController(
    FarmerOrderService service,
    ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SellerOrderQuery query,
        CancellationToken cancellationToken) =>
        Respond(await service.GetOrdersAsync(actor.UserId, query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetOrderAsync(actor.UserId, id, cancellationToken));

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, "confirm", request, cancellationToken));

    [HttpPost("{id:long}/ready")]
    public async Task<IActionResult> Ready(long id, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, "ready", request, cancellationToken));

    [HttpPost("{id:long}/start-shipping")]
    public async Task<IActionResult> StartShipping(long id, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, "dispatch", request, cancellationToken));

    [HttpPost("{id:long}/dispatch")]
    public async Task<IActionResult> Dispatch(long id, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, "dispatch", request, cancellationToken));

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, "reject", request, cancellationToken));

    [HttpGet("{id:long}/packing-slip")]
    public async Task<IActionResult> PackingSlip(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetPackingSlipAsync(actor.UserId, id, cancellationToken));

    [HttpPost("{id:long}/{operation}")]
    public async Task<IActionResult> Perform(long id, string operation, [FromBody] OrderActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ProcessActionAsync(actor.UserId, id, operation, request, cancellationToken));
}
