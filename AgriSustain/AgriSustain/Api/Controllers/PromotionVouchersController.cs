using AgriSustain.Application.DTOs.Promotions;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-PS-01 Manage Promotions and Vouchers.</summary>
[ApiController]
[Authorize(Roles = "PromotionStaff,Promotion Staff")]
[Route("api/promotions/vouchers")]
public sealed class PromotionVouchersController : ControllerBase
{
    private readonly IVoucherManagementService _voucherService;

    /// <summary>
    /// Receives the voucher service from dependency injection.
    /// </summary>
    /// <param name="voucherService">UC-PS-01 application service.</param>
    public PromotionVouchersController(IVoucherManagementService voucherService)
    {
        _voucherService = voucherService;
    }

    /// <summary>
    /// GET api/promotions/vouchers — platform voucher list (Promotion Staff only).
    /// </summary>
    /// <param name="query">Filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the page.</returns>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] VoucherQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.SearchAsync(query, cancellationToken), HttpContext);

    /// <summary>
    /// GET api/promotions/vouchers/{voucherId} — one voucher.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 404.</returns>
    [HttpGet("{voucherId:long}")]
    public async Task<IActionResult> Detail(
        long voucherId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.GetAsync(voucherId, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/promotions/vouchers — create a platform voucher.
    /// </summary>
    /// <param name="request">Voucher values.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with MSG-PV-08; 400 / 409.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.CreateAsync(request, cancellationToken), HttpContext);

    /// <summary>
    /// PUT api/promotions/vouchers/{voucherId} — edit a voucher.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="request">Voucher values with Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with MSG-PV-08; 400 / 404 / 409.</returns>
    [HttpPut("{voucherId:long}")]
    public async Task<IActionResult> Update(
        long voucherId,
        [FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.UpdateAsync(voucherId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/promotions/vouchers/{voucherId}/activate — reactivate a deactivated voucher.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 404 / 409.</returns>
    [HttpPost("{voucherId:long}/activate")]
    public async Task<IActionResult> Activate(
        long voucherId,
        [FromQuery] string? version,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.SetActiveAsync(voucherId, true, version, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/promotions/vouchers/{voucherId}/deactivate — stop a voucher.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 404 / 409.</returns>
    [HttpPost("{voucherId:long}/deactivate")]
    public async Task<IActionResult> Deactivate(
        long voucherId,
        [FromQuery] string? version,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.SetActiveAsync(voucherId, false, version, cancellationToken),
            HttpContext);

    /// <summary>
    /// DELETE api/promotions/vouchers/{voucherId} — delete a voucher never used.
    /// </summary>
    /// <param name="voucherId">Platform voucher id.</param>
    /// <param name="version">Version the screen showed; when sent and different, the action is refused (MSG-PV-15).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 404 / 409.</returns>
    [HttpDelete("{voucherId:long}")]
    public async Task<IActionResult> Delete(
        long voucherId,
        [FromQuery] string? version,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.DeleteAsync(voucherId, version, cancellationToken), HttpContext);
}
