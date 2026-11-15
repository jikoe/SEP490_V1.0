using System.Globalization;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance/farmer-payouts")]
[ServiceFilter(typeof(FinancePayoutInputFilter), Order = int.MinValue)]
public sealed class FinanceFarmerPayoutController(FinanceFarmerPayoutService service, ICurrentActor actor) : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FarmerPayoutQuery query, CancellationToken ct) =>
        Respond(await service.GetPayoutsAsync(actor.UserId, query, ct));

    [HttpGet("farmers")]
    public async Task<IActionResult> Farmers(CancellationToken ct) => Respond(await service.FarmersAsync(actor.UserId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) => Respond(await service.GetContextAsync(actor.UserId, id, ct));

    [HttpGet("{id:long}/qr-preview")]
    public async Task<IActionResult> PaymentQrPreview(long id, CancellationToken ct) =>
        Respond(await service.GetPaymentQrPreviewAsync(actor.UserId, id, ct));

    [HttpPost("{id:long}/process")]
    public async Task<IActionResult> ProcessPayout(long id, ProcessFarmerPayoutRequest request, CancellationToken ct) =>
        Respond(await service.ProcessPayoutAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> RejectRequest(long id, RejectFarmerPayoutRequest request, CancellationToken ct) =>
        Respond(await service.RejectRequestAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/reconcile")]
    public async Task<IActionResult> ReconcilePayout(long id, ReconcileFarmerPayoutRequest request, CancellationToken ct) =>
        Respond(await service.ReconcilePayoutAsync(actor.UserId, id, request, ct));

    [AllowAnonymous]
    [HttpPost("sepay-webhook")]
    [RequestSizeLimit(100000)]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayload payload, CancellationToken ct)
    {
        if (!service.AuthenticateWebhook(Request.Headers.Authorization.ToString(), Request.Headers["x-api-key"].ToString()))
            return Unauthorized();
        if (payload.Id is not > 0 || !string.Equals(payload.TransferType, "out", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false });
        var transfer = PayoutWebhookReader.Read(payload);
        if (transfer == null) return BadRequest(new { success = false });
        await service.RecordOutgoingAsync(transfer, ct);
        return Ok(new { success = true });
    }
}

public sealed class FinancePayoutInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid) context.Result = new BadRequestObjectResult(new ProblemDetails
        { Status = 400, Title = FinancePayoutMessages.Text("MSG172") });
    }
    public void OnActionExecuted(ActionExecutedContext context) { }
}

internal static class PayoutWebhookReader
{
    internal static PayoutOutgoingTransfer? Read(SePayWebhookPayload payload)
    {
        if (payload.Id is not > 0 || payload.TransferAmount <= 0
            || string.IsNullOrWhiteSpace(payload.AccountNumber) || string.IsNullOrWhiteSpace(payload.BankGateway)
            || !DateTime.TryParseExact(payload.TransactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var at)) return null;
        return new("SEPAY-OUT-" + payload.Id, payload.BankGateway, payload.AccountNumber,
            payload.TransferAmount, new DateTimeOffset(at, TimeSpan.FromHours(7)).UtcDateTime,
            string.Join(" ", new[] { payload.Code, payload.Content, payload.Description }.Where(x => !string.IsNullOrWhiteSpace(x))),
            payload.ReferenceCode, JsonSerializer.Serialize(payload));
    }
}
