using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[AllowAnonymous]
[MarketplaceValidation]
[Route("api/marketplace")]
public sealed class MarketplaceController(IMarketplaceService service) : ApiControllerBase
{
    [HttpGet("products")]
    public async Task<IActionResult> BrowseAsync(
        [FromQuery] BrowseMarketplaceRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.BrowseAsync(request, cancellationToken));

    [HttpGet("filters")]
    public async Task<IActionResult> GetFiltersAsync(CancellationToken cancellationToken,
        [FromQuery] long? categoryId = null, [FromQuery] long? supplierId = null) =>
        ToActionResult(await service.GetFiltersAsync(cancellationToken, categoryId, supplierId));

    [HttpGet("products/{productId:long}")]
    public async Task<IActionResult> GetDetailAsync(
        long productId, CancellationToken cancellationToken) =>
        ToActionResult(await service.GetDetailAsync(productId, cancellationToken));
}
