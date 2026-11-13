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
    /// <summary>
    /// GET api/checkout — data needed to open the checkout screen.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the context; 403 for other roles.</returns>
    [HttpGet]
    public async Task<IActionResult> Context(CancellationToken cancellationToken)
    {
        return Respond(await service.GetContextAsync(cancellationToken));
    }

    /// <summary>
    /// GET api/checkout/areas — configured delivery areas.
    /// </summary>
    /// <returns>200 with the areas.</returns>
    [HttpGet("areas")]
    public IActionResult Areas() => Ok(service.GetAreas());

    /// <summary>
    /// GET api/checkout/provinces — provinces of the Goship address catalogue.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the provinces, or 503 ADDRESS_CATALOG_UNAVAILABLE when Goship cannot be reached.</returns>
    [HttpGet("provinces")]
    public async Task<IActionResult> Provinces(CancellationToken cancellationToken) =>
        AddressCatalogResults.Provinces(await addresses.GetProvincesAsync(cancellationToken), HttpContext);

    /// <summary>
    /// GET api/checkout/provinces/{code}/districts — districts of a province.
    /// </summary>
    /// <param name="code">Goship province code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the districts (empty when unknown).</returns>
    [HttpGet("provinces/{code}/districts")]
    public async Task<IActionResult> Districts(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetDistrictsAsync(code, cancellationToken));

    /// <summary>
    /// GET api/checkout/districts/{code}/wards — wards of a district.
    /// </summary>
    /// <param name="code">Goship district code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the wards (empty when unknown).</returns>
    [HttpGet("districts/{code}/wards")]
    public async Task<IActionResult> Wards(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetWardsAsync(code, cancellationToken));

    /// <summary>
    /// POST api/checkout/quotes — calculate and store the order summary.
    /// </summary>
    /// <param name="request">Checkout form values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the quote; 400 / 403 / 409.</returns>
    [HttpPost("quotes")]
    public async Task<IActionResult> Quote(CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.QuoteAsync(request, cancellationToken));
    }

    /// <summary>
    /// POST api/checkout/orders — place the order from an accepted quote.
    /// A new order answers 201 Created with its location; a repeated request answers 200 with the same order.
    /// </summary>
    /// <param name="request">Quote id, request key and confirmation.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>201 / 200 with the order; 400 / 403 / 404 / 409.</returns>
    [HttpPost("orders")]
    public async Task<IActionResult> Place(PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.PlaceAsync(request, cancellationToken);
        return result.IsSuccess && !result.Value!.IsDuplicate
            ? CreatedAtAction(nameof(Order), new { id = result.Value.Id }, result.Value)
            : Respond(result);
    }

    /// <summary>
    /// GET api/checkout/orders/{id} — one order of the current customer.
    /// </summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the order; 403 / 404.</returns>
    [HttpGet("orders/{id:long}")]
    public async Task<IActionResult> Order(long id, CancellationToken cancellationToken)
    {
        return Respond(await service.GetOrderAsync(id, cancellationToken));
    }
}
