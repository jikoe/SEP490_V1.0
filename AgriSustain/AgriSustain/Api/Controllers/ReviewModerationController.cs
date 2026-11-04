using AgriSustain.Application.Services.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-CM-02 Moderate Product Review (Content Moderator).</summary>
[ApiController]
[Authorize(Roles = "ContentModerator,Content Moderator")]
[Route("api/moderation/reviews")]
public sealed class ReviewModerationController(IReviewModerationService service) : ControllerBase
{
    /// <summary>tab = Flagged (queue, default) | Hidden.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tab,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        ApiResults.From(await service.ListAsync(new ReviewQuery(tab, page, pageSize), cancellationToken),
            HttpContext);

    [HttpGet("{reviewId:long}")]
    public async Task<IActionResult> Get(long reviewId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetAsync(reviewId, cancellationToken), HttpContext);

    /// <summary>decision = Hide (reason required) | KeepVisible | Restore.</summary>
    [HttpPost("{reviewId:long}/decision")]
    public async Task<IActionResult> Decide(long reviewId, [FromBody] ModerateReviewRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(await service.ModerateAsync(reviewId, request, cancellationToken), HttpContext);
}
