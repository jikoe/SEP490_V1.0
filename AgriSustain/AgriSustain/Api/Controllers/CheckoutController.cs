using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "RetailCustomer")]
[Route("api/checkout")]
public sealed class CheckoutController(
    ICheckoutService service, IAddressCatalog addresses) : UseCaseControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Context(CancellationToken cancellationToken)
    {
        return Respond(await service.GetContextAsync(cancellationToken));
    }

    [HttpGet("areas")]
    public IActionResult Areas() => Ok(service.GetAreas());

    [HttpGet("provinces")]
    public IActionResult Provinces() => Ok(addresses.GetProvinces());

    [HttpGet("provinces/{code}/wards")]
    public IActionResult Wards(string code) => Ok(addresses.GetWards(code));

    [HttpPost("quotes")]
    public async Task<IActionResult> Quote(CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.QuoteAsync(request, cancellationToken));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> Place(PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.PlaceAsync(request, cancellationToken);
        return result.IsSuccess && !result.Value!.IsDuplicate
            ? CreatedAtAction(nameof(Order), new { id = result.Value.Id }, result.Value)
            : Respond(result);
    }

    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Order(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetOrderAsync(id, cancellationToken));
    }
}
