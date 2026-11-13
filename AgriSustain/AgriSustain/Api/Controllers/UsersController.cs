using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "Administrator")]
[Route("api/users")]
public sealed class UsersController(IAdministrationService service) : UseCaseControllerBase
{
    /// <summary>
    /// GET api/users — account list with filters and paging (Administrator only).
    /// </summary>
    /// <param name="search">Filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the page; 400 / 403.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserSearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchUsersAsync(search, cancellationToken));
    }

    /// <summary>
    /// GET api/users/{id} — one account.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the account; 403 / 404.</returns>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetUserAsync(id, cancellationToken));
    }

    /// <summary>
    /// GET api/users/{id}/action-impact — open orders, contracts and payouts shown before a Suspend.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the impact; 403 / 404.</returns>
    [HttpGet("{id:long}/action-impact")]
    public async Task<IActionResult> ActionImpact(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetAccountImpactAsync(id, cancellationToken));
    }

    /// <summary>
    /// POST api/users/{id}/status-actions — Suspend, Reactivate or Unlock after confirmation.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="request">Action, reason, confirmation and the status / lock shown on screen.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the result; 400 MSG-UM-05; 403 MSG-UM-03 / MSG-UM-13; 404; 409 MSG-UM-04 / MSG-UM-06.</returns>
    [HttpPost("{id:long}/status-actions")]
    public async Task<IActionResult> Change(long id, AccountActionRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.ChangeAccountAsync(id, request, cancellationToken));
    }
}
