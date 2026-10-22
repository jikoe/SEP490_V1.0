using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Route("api/cart")]
[Authorize(Roles = "RetailCustomer")]
public sealed class CartsController(CartService service, ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(actor.UserId, cancellationToken));

    [HttpPost("items")]
    public async Task<IActionResult> Add(CartItemRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.AddItemAsync(actor.UserId, request, cancellationToken));

    [HttpPut("items/{id:long}/quantity")]
    public async Task<IActionResult> Quantity(long id, QuantityRequest request,
        CancellationToken cancellationToken) => Respond(await service.UpdateQuantityAsync(
            actor.UserId, id, request.Quantity, cancellationToken));

    [HttpPut("selection")]
    public async Task<IActionResult> Selection(CartSelectionRequest request,
        CancellationToken cancellationToken) => Respond(await service.SetSelectionAsync(
            actor.UserId, request, false, cancellationToken));

    [HttpPost("remove-items")]
    public async Task<IActionResult> Remove(RemoveCartItemsRequest request,
        CancellationToken cancellationToken) => Respond(await service.SetSelectionAsync(
            actor.UserId, new CartSelectionRequest(request.ItemIds, false), true,
            cancellationToken));

    [HttpPost("prepare-checkout")]
    public async Task<IActionResult> Prepare(PrepareCheckoutRequest request,
        CancellationToken cancellationToken) => Respond(await service.PrepareCheckoutAsync(
            actor.UserId, request.ReviewToken, cancellationToken));
}

