using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Route("api/cart")]
[Authorize(Roles = "RetailCustomer")]
public sealed class RetailCartController(RetailCartService service, ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetCartAsync(actor.UserId, cancellationToken));

    [HttpPost("items")]
    public async Task<IActionResult> Add(CartItemRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.AddItemAsync(actor.UserId, request, cancellationToken));

    [HttpPut("items/{id:long}/quantity")]
    public async Task<IActionResult> Quantity(long id, QuantityRequest request,
        CancellationToken cancellationToken) => Respond(await service.ChangeQuantityAsync(
            actor.UserId, id, request.Quantity, cancellationToken));

    [HttpPut("selection")]
    public async Task<IActionResult> Selection(CartSelectionRequest request,
        CancellationToken cancellationToken) => Respond(await service.SelectItemsAsync(
            actor.UserId, request, cancellationToken));

    [HttpPost("remove-items")]
    public async Task<IActionResult> Remove(RemoveCartItemsRequest request,
        CancellationToken cancellationToken) => Respond(await service.RemoveItemsAsync(
            actor.UserId, request.ItemIds, cancellationToken));

    [HttpPost("prepare-checkout")]
    public async Task<IActionResult> Prepare(PrepareCheckoutRequest request,
        CancellationToken cancellationToken) => Respond(await service.PrepareCheckoutAsync(
            actor.UserId, request.ReviewToken, cancellationToken));
}
