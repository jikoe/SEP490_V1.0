using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Binds the thin UC-ADM-03 HTTP endpoints to the application abstraction; performs no repository
/// or SQL work.
/// </summary>
[ApiController]
[Authorize(Policy = "Administration")]
[Route("api/users")]
public sealed class UsersController(IAccountStatusService service) : ControllerBase
{
    /// <summary>
    /// Binds administrator filters and delegates paging/authorization to UC-ADM-03; maps the
    /// Result without exposing domain entities.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] UserQuery query, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SearchAsync(query, cancellationToken), HttpContext);

    /// <summary>
    /// Delegates one permitted account detail lookup and maps not-found/forbidden outcomes
    /// through the shared API result adapter.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetAsync(id, cancellationToken), HttpContext);

    /// <summary>
    /// Passes a confirmed account status/unlock command to the use case; actor identity is taken
    /// from authentication, not the request body.
    /// </summary>
    [HttpPost("{id:long}/status-actions")]
    public async Task<IActionResult> Change(
        long id, AccountActionRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.ChangeAsync(id, request, cancellationToken), HttpContext);
}
