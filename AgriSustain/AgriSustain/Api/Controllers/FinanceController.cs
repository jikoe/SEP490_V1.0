using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance")]
public sealed class FinanceController(IFinanceService finance, FinanceSampleService samples)
    : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] FinancePeriodQuery query,
        CancellationToken ct, [FromQuery] bool sample = false) =>
        ApiResults.From(await Select(sample).DashboardAsync(query, ct), HttpContext);

    [HttpGet("dashboard/records")]
    public async Task<IActionResult> Records([FromQuery] FinanceRecordsQuery query,
        CancellationToken ct, [FromQuery] bool sample = false) =>
        ApiResults.From(await Select(sample).RecordsAsync(query, ct), HttpContext);

    [HttpGet("payments")]
    public async Task<IActionResult> Search([FromQuery] FinancePaymentQuery query,
        CancellationToken ct, [FromQuery] bool sample = false) =>
        ApiResults.From(await Select(sample).SearchAsync(query, ct), HttpContext);

    [HttpGet("payments/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct, [FromQuery] bool sample = false) =>
        ApiResults.From(await Select(sample).DetailAsync(id, ct), HttpContext);

    [HttpPost("payments/{id:long}/reconcile")]
    public async Task<IActionResult> Reconcile(long id, [FromBody] ReconcilePaymentRequest request,
        CancellationToken ct, [FromQuery] bool sample = false) =>
        ApiResults.From(await Select(sample).ReconcileAsync(id, request, ct), HttpContext);

    private IFinanceService Select(bool sample) => sample ? samples : finance;
}
