using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-ADM-01: read-only platform summary and the pending work of the operational roles.</summary>
[Authorize(Policy = "Administrator")]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController(IPlatformDashboardService service) : UseCaseControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] PlatformDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return Respond(await service.GetAsync(query, cancellationToken));
    }

    [HttpGet("pending-work/{type}/{referenceId:long}")]
    public async Task<IActionResult> PendingWork(string type, long referenceId,
        CancellationToken cancellationToken)
    {
        return Respond(await service.GetPendingWorkAsync(type, referenceId, cancellationToken));
    }
}
