using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Route("api/farmer/products")]
[Authorize(Roles = "Farmer")]
public sealed class ProductsController(ProductService service, ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken cancellationToken) =>
        Ok(await service.OptionsAsync(actor.UserId, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ProductQuery query,
        CancellationToken cancellationToken) =>
        Respond(await service.GetListAsync(actor.UserId, query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetAsync(actor.UserId, id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Submit(ProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(actor.UserId, null, request, cancellationToken);
        return result.IsSuccess && result.Value is { } value
            ? CreatedAtAction(nameof(Detail), new { id = value.ProductId }, value) : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, ProductRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.SaveAsync(actor.UserId, id, request, cancellationToken));

    [HttpPut("{id:long}/visibility")]
    public async Task<IActionResult> Visibility(long id, VisibilityRequest request,
        CancellationToken cancellationToken) => Respond(await service.SetVisibilityAsync(
            actor.UserId, id, request.IsPublished, request.Version, cancellationToken));
}

