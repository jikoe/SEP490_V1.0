using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[AllowAnonymous]
[Route("api/b2b/suppliers")]
public sealed class SupplierCapacityController(ISupplierCapacityService capacityService) : ApiControllerBase
{
    private readonly ISupplierCapacityService _capacityService = capacityService;

    /// <summary>
    /// UC-B2B: Xem hồ sơ năng lực sản xuất và độ tin cậy của nhà cung cấp nông sản.
    /// </summary>
    [HttpGet("{farmerProfileId:long}/capacity-profile")]
    public async Task<IActionResult> GetCapacityProfileAsync(
        long farmerProfileId,
        CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetCapacityProfileAsync(farmerProfileId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xem hồ sơ năng lực theo UserId của nông dân / HTX.
    /// </summary>
    [HttpGet("by-user/{userId:long}/capacity-profile")]
    public async Task<IActionResult> GetCapacityProfileByUserIdAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetCapacityProfileByUserIdAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Danh bạ nhà cung cấp B2B có thể tra cứu theo từ khóa, tỉnh thành.
    /// </summary>
    [HttpGet("directory")]
    public async Task<IActionResult> GetDirectoryAsync(
        [FromQuery] string? keyword,
        [FromQuery] string? province,
        CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetDirectoryAsync(keyword, province, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// AF1: Xem hồ sơ thẩm định chi tiết của chứng chỉ chất lượng (VietGAP, GlobalGAP, OCOP, Organic).
    /// </summary>
    [HttpGet("certificates/{certificateId:long}/dossier")]
    public async Task<IActionResult> GetCertificateDossierAsync(
        long certificateId,
        CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetCertificateDossierAsync(certificateId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// AF2: Xem nhật ký canh tác minh bạch theo mùa vụ / lô nông sản.
    /// </summary>
    [HttpGet("seasons/{cropSeasonId:long}/logs")]
    public async Task<IActionResult> GetSeasonCultivationLogsAsync(
        long cropSeasonId,
        CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetSeasonCultivationLogsAsync(cropSeasonId, cancellationToken);
        return ToActionResult(result);
    }
}

