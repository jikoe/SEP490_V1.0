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
    public async Task<IActionResult> Provinces(CancellationToken cancellationToken) =>
        Ok(await addresses.GetProvincesAsync(cancellationToken));

    [HttpGet("provinces/{code}/districts")]
    public async Task<IActionResult> Districts(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetDistrictsAsync(code, cancellationToken));

    [HttpGet("districts/{code}/wards")]
    public async Task<IActionResult> Wards(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetWardsAsync(code, cancellationToken));

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
