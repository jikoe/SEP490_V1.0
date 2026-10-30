using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-FIN-03 Process Refund.</summary>
[ApiController]
[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance/refunds")]
public sealed class FinanceRefundsController : ControllerBase
{
    private readonly IRefundProcessingService _refundService;

    public FinanceRefundsController(IRefundProcessingService refundService)
    {
        _refundService = refundService;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] RefundQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _refundService.SearchAsync(query, cancellationToken), HttpContext);

    [HttpGet("{refundId:long}")]
    public async Task<IActionResult> Detail(
        long refundId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _refundService.GetDetailAsync(refundId, cancellationToken), HttpContext);

    [HttpPost("{refundId:long}/complete")]
    public async Task<IActionResult> Complete(
        long refundId,
        [FromBody] CompleteRefundRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.CompleteAsync(refundId, request, cancellationToken),
            HttpContext);

    [HttpPost("{refundId:long}/transfer-failed")]
    public async Task<IActionResult> TransferFailed(
        long refundId,
        [FromBody] RefundTransferFailedRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.RecordTransferFailureAsync(refundId, request, cancellationToken),
            HttpContext);
}
