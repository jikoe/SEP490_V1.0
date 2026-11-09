using System.Security.Claims;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Route("api/b2b/orders")]
public sealed class WholesaleOrderController(IWholesaleOrderService orderService) : ApiControllerBase
{
    private const long DefaultDemoWholesaleBuyerUserId = 15; // wholesale1 (Approved KYC)
    private readonly IWholesaleOrderService _orderService = orderService;

    /// <summary>
    /// UC-B2B-01: Lấy thông tin nông sản, bậc giá sỉ và hồ sơ KYC doanh nghiệp của người mua.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("product-info/{productId:long}")]
    [HttpGet("product/{productId:long}")]
    public async Task<IActionResult> GetWholesaleProductInfoAsync(
        long productId,
        CancellationToken cancellationToken)
    {
        var buyerUserId = ResolveBuyerUserId();
        var result = await _orderService.GetWholesaleProductInfoAsync(productId, buyerUserId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// AF2: Tính toán tự động bậc giá sỉ, chiết khấu, VAT và phí logistics theo số lượng đặt mua thực tế.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("calculate-price")]
    [HttpPost("calculate-pricing")]
    public async Task<IActionResult> CalculatePricingAsync(
        [FromBody] WholesalePriceCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.CalculatePricingAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Tạo đơn hàng sỉ B2B và thực hiện giữ chỗ (Reserve) lô nông sản thu hoạch.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("create")]
    [HttpPost]
    public async Task<IActionResult> CreateWholesaleOrderAsync(
        [FromBody] CreateWholesaleOrderRequest request,
        CancellationToken cancellationToken)
    {
        var buyerUserId = ResolveBuyerUserId();
        var result = await _orderService.CreateWholesaleOrderAsync(request, buyerUserId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ đơn hàng sỉ đã đặt và thống kê theo Nhà bán (Nông hộ) cho Dashboard & Sidebar.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("my-orders")]
    public async Task<IActionResult> GetBuyerWholesaleOrdersOverviewAsync(CancellationToken cancellationToken)
    {
        var buyerUserId = ResolveBuyerUserId();
        var result = await _orderService.GetBuyerWholesaleOrdersOverviewAsync(buyerUserId, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Lấy chi tiết một đơn hàng sỉ theo mã đơn (hỗ trợ xem detail và polling trạng thái thanh toán SePay).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("by-code/{orderCode}")]
    [HttpGet("detail/{orderCode}")]
    public async Task<IActionResult> GetWholesaleOrderDetailByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.GetWholesaleOrderDetailByCodeAsync(orderCode, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xác nhận thanh toán SePay cho đơn hàng sỉ (hoặc đồng bộ khi người mua hoàn tất chuyển khoản QR).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{orderCode}/confirm-sepay")]
    public async Task<IActionResult> ConfirmSePayWholesaleOrderAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.ConfirmSePayWholesaleOrderAsync(orderCode, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Tự động hủy đơn hàng mua sỉ và hoàn trả hạn ngạch tồn kho lô hàng khi hết thời gian chờ thanh toán SePay.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{orderCode}/cancel-expired")]
    public async Task<IActionResult> CancelExpiredWholesaleOrderAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.CancelExpiredWholesaleOrderAsync(orderCode, cancellationToken);
        return ToActionResult(result);
    }

    private long ResolveBuyerUserId()
    {
        var nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrEmpty(nameId) &&
            long.TryParse(nameId, out var parsedId) &&
            parsedId > 0 &&
            string.Equals(role, "WholesaleBuyer", StringComparison.OrdinalIgnoreCase))
        {
            return parsedId;
        }

        return DefaultDemoWholesaleBuyerUserId;
    }
}

