using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "Administrator")]
[Route("api/categories")]
public sealed class CategoriesController(IAdministrationService service) : UseCaseControllerBase
{
    /// <summary>
    /// GET api/categories — category list with filters and paging (Administrator only).
    /// </summary>
    /// <param name="search">Filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the page; 400 / 403.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CategorySearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchCategoriesAsync(search, cancellationToken));
    }

    /// <summary>
    /// GET api/categories/{id} — one category.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 403 / 404.</returns>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetCategoryAsync(id, cancellationToken));
    }

    /// <summary>
    /// POST api/categories — create a category; answers 201 Created with its location.
    /// </summary>
    /// <param name="request">Category values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>201 with the category; 400 / 403.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SaveCategoryAsync(null, request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(Detail), new { id = result.Value!.Id },
            result.Value) : Respond(result);
    }

    /// <summary>
    /// PUT api/categories/{id} — edit a category.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="request">Category values with the version shown.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CategoryRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SaveCategoryAsync(id, request, cancellationToken));
    }

    // "action" is a reserved MVC route value, so the operation segment needs its own name.
    // BR-CAT-04 has no separate archive action: delete archives a category that has been used.
    /// <summary>
    /// POST api/categories/{id}/{activate|deactivate|delete} — status change or delete after confirmation.
    /// There is no separate archive action: delete archives a category that has been used (BR-CAT-04).
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="operation">activate, deactivate or delete.</param>
    /// <param name="request">Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the outcome; 400 / 403 / 404 / 409.</returns>
    [HttpPost("{id:long}/{operation:regex(^(activate|deactivate|delete)$)}")]
    public async Task<IActionResult> Change(long id, string operation, ConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.ChangeCategoryAsync(id, operation, request, cancellationToken));
    }
}
