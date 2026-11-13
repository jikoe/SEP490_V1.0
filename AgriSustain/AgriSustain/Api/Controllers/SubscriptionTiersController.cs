using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-ADM-09 Manage Subscription Tiers.</summary>
[Authorize(Policy = "Administrator")]
[Route("api/admin/subscription-tiers")]
public sealed class SubscriptionTiersController(ISubscriptionTierService service) : UseCaseControllerBase
{
    /// <summary>
    /// GET api/admin/subscription-tiers — tier list (Administrator only).
    /// </summary>
    /// <param name="search">Filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the page; 400 / 403.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SubscriptionTierSearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchAsync(search, cancellationToken));
    }

    /// <summary>
    /// GET api/admin/subscription-tiers/{id} — one tier.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 403 / 404.</returns>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetAsync(id, cancellationToken));
    }

    /// <summary>
    /// POST api/admin/subscription-tiers — create a tier.
    /// </summary>
    /// <param name="request">Tier values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with MSG-ST-01; 400 / 403.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionTierRequest request, CancellationToken cancellationToken)
    {
        return Respond(await service.SaveAsync(null, request, cancellationToken));
    }

    /// <summary>
    /// PUT api/admin/subscription-tiers/{id} — edit a tier.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="request">Tier values with the version shown.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with MSG-ST-01; 400 / 403 / 404 / 409.</returns>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SubscriptionTierRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SaveAsync(id, request, cancellationToken));
    }

    // "action" is a reserved MVC route value, so the operation segment needs its own name.
    /// <summary>
    /// POST api/admin/subscription-tiers/{id}/{activate|deactivate} — status change after confirmation.
    /// The segment is called "operation" because "action" is a reserved MVC route value.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="operation">activate or deactivate.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPost("{id:long}/{operation:regex(^(activate|deactivate)$)}")]
    public async Task<IActionResult> ChangeStatus(long id, string operation, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SetActiveAsync(id, operation == "activate", request, cancellationToken));
    }

    /// <summary>
    /// POST api/admin/subscription-tiers/{id}/delete — delete a tier never purchased.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409 TIER_PURCHASED.</returns>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.DeleteAsync(id, request, cancellationToken));
    }
}
