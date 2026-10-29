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

    public PromotionVouchersController(IVoucherManagementService voucherService)
    {
        _voucherService = voucherService;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] VoucherQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.SearchAsync(query, cancellationToken), HttpContext);

    [HttpGet("{voucherId:long}")]
    public async Task<IActionResult> Detail(
        long voucherId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.GetAsync(voucherId, cancellationToken), HttpContext);

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.CreateAsync(request, cancellationToken), HttpContext);

    [HttpPut("{voucherId:long}")]
    public async Task<IActionResult> Update(
        long voucherId,
        [FromBody] SaveVoucherRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.UpdateAsync(voucherId, request, cancellationToken),
            HttpContext);

    [HttpPost("{voucherId:long}/activate")]
    public async Task<IActionResult> Activate(
        long voucherId,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.SetActiveAsync(voucherId, true, cancellationToken),
            HttpContext);

    [HttpPost("{voucherId:long}/deactivate")]
    public async Task<IActionResult> Deactivate(
        long voucherId,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _voucherService.SetActiveAsync(voucherId, false, cancellationToken),
            HttpContext);

    [HttpDelete("{voucherId:long}")]
    public async Task<IActionResult> Delete(
        long voucherId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _voucherService.DeleteAsync(voucherId, cancellationToken), HttpContext);
}
