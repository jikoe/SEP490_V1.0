using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[Route("api/farmer/orders")]
public sealed class SellerOrdersController(OrderFulfillmentService service, ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SellerOrderQuery query,
        CancellationToken cancellationToken) =>
        Respond(await service.GetListAsync(actor.UserId, query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetAsync(actor.UserId, id, cancellationToken));

    [HttpPost("{id:long}/{operation}")]
    public async Task<IActionResult> Perform(long id, string operation, OrderActionRequest request,
        CancellationToken cancellationToken) => Respond(await service.ActAsync(
            actor.UserId, id, operation, request, cancellationToken));
}

