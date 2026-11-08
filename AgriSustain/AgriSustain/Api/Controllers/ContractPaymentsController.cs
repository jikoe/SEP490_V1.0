using AgriSustain.Application.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-FIN-06 Track B2B Contract Payments (Finance Staff).</summary>
[ApiController]
[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance/contract-payments")]
public sealed class ContractPaymentsController(IContractPaymentService service) : ControllerBase
{
    /// <summary>status = OnTrack | DueSoon | Overdue | Completed. 10 contracts per page.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, CancellationToken cancellationToken = default) =>
        ApiResults.From(await service.ListAsync(new ContractPaymentQuery(search, status, page), cancellationToken),
            HttpContext);

    /// <summary>Payment schedule and history of one contract (Step 4).</summary>
    [HttpGet("{contractId:long}")]
    public async Task<IActionResult> Schedule(long contractId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetScheduleAsync(contractId, cancellationToken), HttpContext);

    /// <summary>Step 5-8: confirm a bank transfer that arrived without a matching reference.</summary>
    [HttpPost("lines/{lineId:long}/confirm")]
    public async Task<IActionResult> Confirm(long lineId, [FromBody] ConfirmReceivedRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.ConfirmReceivedAsync(lineId, request, cancellationToken), HttpContext);

    /// <summary>AF3: mark an unpaid line past its due date as Overdue, with a note.</summary>
    [HttpPost("lines/{lineId:long}/overdue")]
    public async Task<IActionResult> Overdue(long lineId, [FromBody] MarkOverdueRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.MarkOverdueAsync(lineId, request, cancellationToken), HttpContext);

    /// <summary>AF4: e-mail a payment reminder (once per line every 24 hours).</summary>
    [HttpPost("lines/{lineId:long}/remind")]
    public async Task<IActionResult> Remind(long lineId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SendReminderAsync(lineId, cancellationToken), HttpContext);
}
