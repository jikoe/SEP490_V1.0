using AgriSustain.Application.Services.Traceability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-TRC-01 View Product Traceability: public, read-only (Guest, RetailCustomer, WholesaleBuyer).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/traceability")]
public sealed class TraceabilityController(ITraceabilityService service) : ControllerBase
{
    /// <summary>code = batch code, QR content or the scanned trace URL.</summary>
    [HttpGet]
    public async Task<IActionResult> Lookup([FromQuery] string? code, CancellationToken cancellationToken) =>
        ApiResults.From(await service.LookupAsync(code, cancellationToken), HttpContext);
}
