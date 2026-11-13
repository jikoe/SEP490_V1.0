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
    /// <summary>
    /// GET api/admin/dashboard — platform summary and pending work for a period (Administrator only).
    /// </summary>
    /// <param name="query">From and To from the query string.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the dashboard; 400 MSG-DB-01; 403.</returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] PlatformDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return Respond(await service.GetAsync(query, cancellationToken));
    }

    /// <summary>
    /// GET api/admin/dashboard/pending-work/{type}/{referenceId} — one pending item, read-only.
    /// </summary>
    /// <param name="type">Kyc, Listing, Complaint or Refund.</param>
    /// <param name="referenceId">Item id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the item; 403 / 404 / 409 MSG-DB-04.</returns>
    [HttpGet("pending-work/{type}/{referenceId:long}")]
    public async Task<IActionResult> PendingWork(string type, long referenceId,
        CancellationToken cancellationToken)
    {
        return Respond(await service.GetPendingWorkAsync(type, referenceId, cancellationToken));
    }
}
