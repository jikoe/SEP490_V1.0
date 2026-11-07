using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance")]
public sealed class FinanceController(IFinanceService finance)
    : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] FinancePeriodQuery query,
        CancellationToken ct) =>
        ApiResults.From(await finance.DashboardAsync(query, ct), HttpContext);

    [HttpGet("dashboard/records")]
    public async Task<IActionResult> Records([FromQuery] FinanceRecordsQuery query,
        CancellationToken ct) =>
        ApiResults.From(await finance.RecordsAsync(query, ct), HttpContext);

    [HttpGet("payments")]
    public async Task<IActionResult> Search([FromQuery] FinancePaymentQuery query,
        CancellationToken ct) =>
        ApiResults.From(await finance.SearchAsync(query, ct), HttpContext);

    [HttpGet("payments/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        ApiResults.From(await finance.DetailAsync(id, ct), HttpContext);

    [HttpPost("payments/{id:long}/reconcile")]
    public async Task<IActionResult> Reconcile(long id, [FromBody] ReconcilePaymentRequest request,
        CancellationToken ct) =>
        ApiResults.From(await finance.ReconcileAsync(id, request, ct), HttpContext);
}
