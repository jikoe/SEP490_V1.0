using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "Administrator")]
[Route("api/categories")]
[RequestSizeLimit(7_100_000)]
public sealed class CategoriesController(IAdministrationService service) : UseCaseControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CategorySearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchCategoriesAsync(search, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetCategoryAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SaveCategoryAsync(null, request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Detail), new { id = result.Value!.Id },
            result.Value) : Respond(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SaveCategoryAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/{action}")]
    public async Task<IActionResult> Change(long id, string action, ConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.ChangeCategoryAsync(id, action, request.Confirmed,
            cancellationToken));
    }
}
