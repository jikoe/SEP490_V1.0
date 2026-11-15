using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "RetailCustomer")]
[Route("api/orders")]
public sealed class OrdersController(
    IOrderTrackingService service,
    BankTransferReconciler? reconciler = null) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrderHistory(
        [FromQuery] OrderQuery query, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetOrderHistoryAsync(query, cancellationToken), HttpContext);

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOrderDetail(
        long id, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetOrderDetailAsync(id, cancellationToken), HttpContext);

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetOrderDetailByCode(
        string code, CancellationToken cancellationToken)
    {
        if (reconciler != null)
        {
            await reconciler.TrySyncIncomingFromSePayApiAsync(cancellationToken);
        }
        return ApiResults.From(await service.GetOrderDetailByCodeAsync(code, cancellationToken), HttpContext);
    }

    [HttpPost("{id:long}/confirm-received")]
    public async Task<IActionResult> ConfirmReceived(
        long id, CancellationToken cancellationToken) =>
        ApiResults.From(await service.ConfirmReceivedAsync(id, cancellationToken), HttpContext);
}
