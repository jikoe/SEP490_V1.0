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
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SubscriptionTierSearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchAsync(search, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionTierRequest request, CancellationToken cancellationToken)
    {
        return Respond(await service.SaveAsync(null, request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SubscriptionTierRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SaveAsync(id, request, cancellationToken));
    }

    // "action" is a reserved MVC route value, so the operation segment needs its own name.
    [HttpPost("{id:long}/{operation:regex(^(activate|deactivate)$)}")]
    public async Task<IActionResult> ChangeStatus(long id, string operation, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SetActiveAsync(id, operation == "activate", request, cancellationToken));
    }

    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, SubscriptionTierChangeRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.DeleteAsync(id, request, cancellationToken));
    }
}
