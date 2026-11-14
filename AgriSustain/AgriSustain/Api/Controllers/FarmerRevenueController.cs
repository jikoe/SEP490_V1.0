using AgriSustain.Application.DTOs.Revenue;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "Farmer")]
[ServiceFilter(typeof(FarmerBusinessInputFilter), Order = int.MinValue)]
[Route("api/farmer/revenue")]
public sealed class FarmerRevenueController(FarmerRevenueService service, ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRevenue([FromQuery] RevenueFilterRequest filter, CancellationToken ct) =>
        Respond(await service.GetRevenueAsync(actor.UserId, filter, ct));

    [HttpGet("transaction/{id}")]
    public async Task<IActionResult> TransactionDetail(string id, CancellationToken ct) =>
        Respond(await service.GetTransactionDetailAsync(actor.UserId, id, ct));

    [HttpGet("export")]
    public async Task<IActionResult> ExportRevenue([FromQuery] RevenueFilterRequest filter, [FromQuery] string format, CancellationToken ct)
    {
        var result = await service.ExportRevenueAsync(actor.UserId, filter, format, ct);
        if (!result.IsSuccess) return Respond(result);
        return File(result.Value!.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("payment-requests")]
    public async Task<IActionResult> PaymentRequests([FromQuery] FarmerPayoutQuery query, CancellationToken ct) =>
        Respond(await service.GetRequestsAsync(actor.UserId, query, ct));

    [HttpGet("payment-requests/context")]
    public async Task<IActionResult> PaymentRequestContext(CancellationToken ct) =>
        Respond(await service.GetRequestContextAsync(actor.UserId, ct));

    [HttpPost("payment-requests/preview")]
    public async Task<IActionResult> PreviewPaymentRequest(CreateFarmerPayoutRequest request, CancellationToken ct) =>
        Respond(await service.PreviewPaymentRequestAsync(actor.UserId, request, ct));

    [HttpPost("payment-requests")]
    public async Task<IActionResult> SubmitPaymentRequest(CreateFarmerPayoutRequest request, CancellationToken ct) =>
        Respond(await service.SubmitPaymentRequestAsync(actor.UserId, request, ct));

    [HttpGet("payment-requests/{id:long}")]
    public async Task<IActionResult> PaymentRequestDetail(long id, CancellationToken ct) =>
        Respond(await service.GetRequestAsync(actor.UserId, id, ct));
}
