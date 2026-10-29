using AgriSustain.Application.DTOs.Moderation;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "ContentModerator,Content Moderator")]
[Route("api/moderation/products")]
public sealed class ProductModerationController : ControllerBase
{
    private readonly IProductModerationService _moderationService;

    public ProductModerationController(IProductModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string status = "Pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _moderationService.GetListAsync(
            keyword,
            status,
            page,
            pageSize,
            cancellationToken);

        return result is null
            ? Problem(
                title: "Trạng thái không hợp lệ",
                detail: "Trạng thái chỉ nhận Pending hoặc Reviewed.",
                statusCode: StatusCodes.Status400BadRequest)
            : Ok(result);
    }

    [HttpGet("{productId:long}")]
    public async Task<IActionResult> Detail(
        long productId,
        CancellationToken cancellationToken)
    {
        var result = await _moderationService.GetDetailAsync(productId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{productId:long}/approve")]
    public Task<IActionResult> Approve(
        long productId,
        CancellationToken cancellationToken)
    {
        return Moderate(productId, approved: true, reason: null, cancellationToken);
    }

    [HttpPost("{productId:long}/reject")]
    public Task<IActionResult> Reject(
        long productId,
        [FromBody] RejectProductRequest request,
        CancellationToken cancellationToken)
    {
        return Moderate(productId, approved: false, request.Reason, cancellationToken);
    }

    private async Task<IActionResult> Moderate(
        long productId,
        bool approved,
        string? reason,
        CancellationToken cancellationToken)
    {
        var outcome = await _moderationService.ModerateAsync(
            productId,
            approved,
            reason,
            cancellationToken);

        return outcome switch
        {
            ProductModerationOutcome.Success => Ok(new
            {
                id = productId,
                status = approved ? "Approved" : "Rejected"
            }),
            ProductModerationOutcome.NotFound => NotFound(),
            ProductModerationOutcome.Conflict => Problem(
                title: "Xung đột trạng thái",
                detail: "Sản phẩm đã được xử lý trước đó.",
                statusCode: StatusCodes.Status409Conflict),
            ProductModerationOutcome.FarmerNotVerified => Problem(
                title: "Nông trại chưa được xác minh",
                detail: "Chỉ có thể duyệt sản phẩm khi hồ sơ nông trại đã được xác minh.",
                statusCode: StatusCodes.Status409Conflict),
            ProductModerationOutcome.InvalidReason => Problem(
                title: "Lý do từ chối không hợp lệ",
                detail: "Lý do từ chối phải có từ 1 đến 500 ký tự.",
                statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException(
                $"Unsupported moderation outcome: {outcome}.")
        };
    }
}
