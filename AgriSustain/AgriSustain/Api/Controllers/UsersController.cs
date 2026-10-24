using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "Administrator")]
[Route("api/users")]
public sealed class UsersController(IAdministrationService service) : UseCaseControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserSearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchUsersAsync(search, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetUserAsync(id, cancellationToken));
    }

    [HttpPost("{id:long}/status-actions")]
    public async Task<IActionResult> Change(long id, AccountActionRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.ChangeAccountAsync(id, request, cancellationToken));
    }
}
