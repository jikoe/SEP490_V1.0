using AgriSustain.Application.Services.B2B;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-WB-08 Manage Buyer Subscription (WholesaleBuyer).</summary>
[ApiController]
[Authorize(Roles = "WholesaleBuyer")]
[Route("api/b2b/subscription")]
public sealed class BuyerSubscriptionController(IBuyerSubscriptionService service) : ControllerBase
{
    /// <summary>Current and scheduled subscription plus the active tiers (Step 2-3).</summary>
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetOverviewAsync(cancellationToken), HttpContext);

    /// <summary>Select, renew, upgrade or downgrade a tier (Step 4-7). Paid tiers return a SePay QR.</summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.PurchaseAsync(request, cancellationToken), HttpContext);

    /// <summary>Polled by the payment screen until the webhook confirms the transfer (Step 9-11).</summary>
    [HttpGet("payments/{reference}")]
    public async Task<IActionResult> Payment(string reference, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetPaymentAsync(reference, cancellationToken), HttpContext);

    /// <summary>Step 12 / AF8 / AF9.</summary>
    [HttpPut("auto-renew")]
    public async Task<IActionResult> AutoRenew([FromBody] AutoRenewRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.SetAutoRenewAsync(request, cancellationToken), HttpContext);

    /// <summary>AF13: cancel a scheduled downgrade before it starts.</summary>
    [HttpDelete("scheduled")]
    public async Task<IActionResult> CancelScheduled(CancellationToken cancellationToken) =>
        ApiResults.From(await service.CancelScheduledAsync(cancellationToken), HttpContext);
}
