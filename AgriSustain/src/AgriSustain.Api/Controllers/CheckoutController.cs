using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Receives checkout and geographic read abstractions for the existing retail checkout screen
/// only.
/// </summary>
[ApiController]
[Authorize(Policy = "RetailCustomerCheckout")]
[Route("api/checkout")]
public sealed class CheckoutController(
    ICheckoutService service, IAddressCatalog addresses) : ControllerBase
{
    /// <summary>
    /// Returns only the authenticated RetailCustomer own checkout context; does not implement
    /// cart editing.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Context(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetContextAsync(cancellationToken), HttpContext);

    /// <summary>
    /// Exposes administrative province options inside the authenticated checkout flow; no
    /// separate address-management UC is added.
    /// </summary>
    [HttpGet("provinces")]
    public IActionResult Provinces() => Ok(addresses.GetProvinces());

    /// <summary>
    /// Exposes wards linked to one province from the same catalog used for server normalization;
    /// an unknown province yields an empty option list.
    /// </summary>
    [HttpGet("provinces/{code}/wards")]
    public IActionResult Wards(string code) => Ok(addresses.GetWards(code));

    /// <summary>
    /// Delegates server-side quote creation, including FEFO allocation and trusted
    /// administrative-code validation, without reserving stock.
    /// </summary>
    [HttpPost("quotes")]
    public async Task<IActionResult> Preview(
        CheckoutInput request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.PreviewAsync(request, cancellationToken), HttpContext);

    /// <summary>
    /// Delegates confirmed placement; returns 201 for a new order or the existing demo retry
    /// result. No QR or payment provider request is sent.
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> Place(
        PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await service.PlaceAsync(request, cancellationToken);
        return result.IsSuccess && result.Value is { IsDuplicate: false } order
            ? StatusCode(201, order) : ApiResults.From(result, HttpContext);
    }
}
