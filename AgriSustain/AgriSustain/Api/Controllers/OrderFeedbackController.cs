using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "RetailCustomer")]
[Route("api/orders")]
public sealed class OrderFeedbackController(
    IOrderFeedbackService service, ICurrentActor actor, IOrderFeedbackStorage storage)
    : ControllerBase
{
    [HttpGet("{orderId:long}/feedback")]
    public async Task<IActionResult> ContextAsync(
        long orderId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetContextAsync(
            actor.UserId, orderId, cancellationToken), HttpContext);

    [HttpPost("{orderId:long}/reviews/{itemId:long}")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> ReviewAsync(long orderId, long itemId,
        [FromForm] ReviewForm form, CancellationToken cancellationToken)
    {
        if (Request.Form.Files.Count > 0)
        {
            return Problem("Đánh giá chỉ hỗ trợ số sao và nhận xét, không đính kèm ảnh.", statusCode: 400);
        }
        return ApiResults.From(await service.ReviewAsync(actor.UserId, orderId, itemId,
            new ReviewSubmission(form.Rating, form.Comment), cancellationToken), HttpContext);
    }

    [HttpPut("{orderId:long}/reviews/{itemId:long}")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> UpdateReviewAsync(long orderId, long itemId,
        [FromForm] ReviewForm form, CancellationToken cancellationToken)
    {
        if (Request.Form.Files.Count > 0)
        {
            return Problem("Đánh giá chỉ hỗ trợ số sao và nhận xét, không đính kèm ảnh.", statusCode: 400);
        }
        return ApiResults.From(await service.UpdateReviewAsync(actor.UserId, orderId, itemId,
            new ReviewSubmission(form.Rating, form.Comment), cancellationToken), HttpContext);
    }

    [HttpPost("{orderId:long}/complaints")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    public Task<IActionResult> ComplainAsync(long orderId, [FromForm] ComplaintForm form,
        CancellationToken cancellationToken) =>
        WithFilesAsync(form.Files, async files => ApiResults.From(await service.ComplainAsync(
            actor.UserId, orderId, new ComplaintSubmission(form.OrderItemIds,
                form.Reason, form.Description), files, cancellationToken), HttpContext));

    [HttpGet("feedback-files/{owner:long}/{key}")]
    public IActionResult Evidence(long owner, string key)
    {
        if (owner != actor.UserId)
        {
            return NotFound();
        }
        var download = storage.OpenEvidence(owner, key);
        return download == null ? NotFound() : File(download.Content, download.ContentType);
    }

    private async Task<IActionResult> WithFilesAsync(
        List<IFormFile> uploads, Func<IReadOnlyList<FeedbackFile>, Task<IActionResult>> action)
    {
        if (uploads.Count > 5
            || uploads.Any(file => file.Length is <= 0 or > 5 * 1024 * 1024))
        {
            return Problem("Chỉ được đính kèm tối đa 5 ảnh, mỗi ảnh không quá 5 MB.",
                statusCode: 400);
        }
        var files = new List<FeedbackFile>();
        try
        {
            foreach (var upload in uploads)
            {
                files.Add(new FeedbackFile(
                    upload.FileName, upload.Length, upload.OpenReadStream()));
            }
            return await action(files);
        }
        finally
        {
            foreach (var file in files)
            {
                await file.Content.DisposeAsync();
            }
        }
    }

    public sealed class ReviewForm
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class ComplaintForm
    {
        public List<long> OrderItemIds { get; set; } = [];
        public string? Reason { get; set; }
        public string? Description { get; set; }
        public List<IFormFile> Files { get; set; } = [];
    }
}
