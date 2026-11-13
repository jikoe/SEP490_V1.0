using AgriSustain.Application.Services.B2B;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-WB-01 Search Suppliers (WholesaleBuyer).</summary>
[ApiController]
[Authorize(Roles = "WholesaleBuyer")]
[Route("api/b2b/suppliers")]
public sealed class SupplierDirectoryController(ISupplierDirectoryService service) : ControllerBase
{
    /// <summary>sort = Reputation (default) | Newest | Area. 12 suppliers per page.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] string? provinceCode,
        [FromQuery] long? categoryId, [FromQuery] string? certificateType, [FromQuery] string? productionScale,
        [FromQuery] string? sort, [FromQuery] int page = 1, CancellationToken cancellationToken = default) =>
        ApiResults.From(await service.SearchAsync(new SupplierSearchQuery(
            keyword, provinceCode, categoryId, certificateType, productionScale, sort, page), cancellationToken),
            HttpContext);

    /// <summary>Values of the filter panel (Step 2).</summary>
    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetFilterOptionsAsync(cancellationToken), HttpContext);
}
