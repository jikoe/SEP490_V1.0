using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/orders")]
public sealed class OrdersController(IOrderTrackingService service) : ControllerBase
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
        string code, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetOrderDetailByCodeAsync(code, cancellationToken), HttpContext);
}
