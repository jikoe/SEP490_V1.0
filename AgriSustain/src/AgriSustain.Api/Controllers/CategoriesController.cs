using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Binds UC-ADM-05 endpoints to the category service without embedding catalog-management or
/// persistence behavior.
/// </summary>
[ApiController]
[Authorize(Policy = "Administration")]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService service) : ControllerBase
{
    /// <summary>
    /// Reads one authorized category for the dedicated details screen, including real reference
    /// counts. Direct links do not depend on loading the first hundred categories in the browser.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(new CategoryQuery(Id: id, PageSize: 1),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResults.From(result, HttpContext);
        }
        var category = result.Value!.Items.SingleOrDefault();
        return category == null
            ? ApiResults.From(Result<CategoryResponse>.Failure(new Error(
                "CATEGORY_NOT_FOUND", "Không tìm thấy danh mục.", "NotFound")), HttpContext)
            : Ok(category);
    }

    /// <summary>
    /// Binds category filters and returns the approved paged read model without
    /// archive/image/Version fields.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] CategoryQuery query, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SearchAsync(query, cancellationToken), HttpContext);

    /// <summary>
    /// Delegates validated category creation and returns HTTP 201 on success; failures retain
    /// their stable ProblemDetails contract.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(3145728)]
    public async Task<IActionResult> Create(
        SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(null, request, cancellationToken);
        return result.IsSuccess
            ? StatusCode(201, new
            {
                id = result.Value
            })
            : ApiResults.From(result, HttpContext);
    }

    /// <summary>
    /// Delegates an update of approved category fields by ID. No image upload or Version field is
    /// required by the revised contract.
    /// </summary>
    [HttpPut("{id:long}")]
    [RequestSizeLimit(3145728)]
    public async Task<IActionResult> Update(
        long id, SaveCategoryRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SaveAsync(id, request, cancellationToken), HttpContext);

    /// <summary>
    /// Delegates activate/deactivate or confirmed physical deletion. References are checked in
    /// the use case and must remain protected by SQL FKs in the real adapter.
    /// </summary>
    [HttpPost("{id:long}/{operation:regex(^(activate|deactivate|delete)$)}")]
    public async Task<IActionResult> Change(
        long id, string operation, CategoryActionRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.ChangeAsync(
            id, operation, request, cancellationToken), HttpContext);
}
