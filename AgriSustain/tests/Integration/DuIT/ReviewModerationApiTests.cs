using System.Net;
using System.Net.Http.Json;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-CM-02 Moderate Product Review (Content Moderator) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-CM-02")]
public sealed class ReviewModerationApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private Task<HttpResponseMessage> Decide(long reviewId, string decision, string? reason) =>
        _api.Moderator().PostAsJsonAsync($"api/moderation/reviews/{reviewId}/decision", new { decision, reason });

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #01] The Flagged tab lists only the review caught by the automatic rules")]
    public async Task List_FlaggedTab_OnlyFlaggedReview()
    {
        var json = await ApiAssert.Status(await _api.Moderator().GetAsync("api/moderation/reviews?tab=Flagged"), HttpStatusCode.OK);

        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(_api.FlaggedReviewId, item.GetProperty("productReviewId").GetInt64());
        Assert.Equal((1, 1), (json.GetProperty("counts").GetProperty("flagged").GetInt32(),
            json.GetProperty("counts").GetProperty("hidden").GetInt32()));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #02] Hiding a review with a reason saves the status, reason and audit entry")]
    public async Task Hide_WithReason_HiddenAndAudited()
    {
        await ApiAssert.Status(await Decide(_api.FlaggedReviewId, "Hide", "Ngôn từ thô tục"), HttpStatusCode.OK);

        await using var db = _api.Open();
        var review = await db.ProductReviews.SingleAsync(r => r.ProductReviewId == _api.FlaggedReviewId);
        Assert.Equal(("Hidden", "Ngôn từ thô tục"), (review.ModerationStatus, review.ModerationReason));
        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "ProductReview" && a.EntityId == _api.FlaggedReviewId);
        Assert.Equal(("ModerateReview:Hide", _api.ModeratorId), (audit.Action, audit.PerformedByUserId));
        var after = await ApiAssert.Status(await _api.Moderator().GetAsync("api/moderation/reviews?tab=Flagged"), HttpStatusCode.OK);
        Assert.Empty(after.GetProperty("items").EnumerateArray());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #03] Hiding without a reason is refused and the review stays visible")]
    public async Task Hide_NoReason_Rejected()
    {
        var response = await Decide(_api.FlaggedReviewId, "Hide", " ");

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "REASON_REQUIRED");
        await using var db = _api.Open();
        Assert.Equal("Approved", (await db.ProductReviews.SingleAsync(r => r.ProductReviewId == _api.FlaggedReviewId)).ModerationStatus);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.EntityType == "ProductReview"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #04] Keep Visible marks the flagged review as reviewed without hiding it")]
    public async Task KeepVisible_Flagged_ReviewedAndVisible()
    {
        await ApiAssert.Status(await Decide(_api.FlaggedReviewId, "KeepVisible", null), HttpStatusCode.OK);

        await using var db = _api.Open();
        Assert.Equal("Approved", (await db.ProductReviews.SingleAsync(r => r.ProductReviewId == _api.FlaggedReviewId)).ModerationStatus);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == _api.FlaggedReviewId && a.Action == "ModerateReview:KeepVisible"));
        var after = await ApiAssert.Status(await _api.Moderator().GetAsync("api/moderation/reviews?tab=Flagged"), HttpStatusCode.OK);
        Assert.Equal(0, after.GetProperty("counts").GetProperty("flagged").GetInt32());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #05] Restoring a hidden review makes it visible again")]
    public async Task Restore_Hidden_Visible()
    {
        await ApiAssert.Status(await Decide(_api.HiddenReviewId, "Restore", null), HttpStatusCode.OK);

        await using var db = _api.Open();
        var review = await db.ProductReviews.SingleAsync(r => r.ProductReviewId == _api.HiddenReviewId);
        Assert.Equal("Approved", review.ModerationStatus);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == _api.HiddenReviewId && a.Action == "ModerateReview:Restore"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #06] Restoring a review that is not hidden returns 409")]
    public async Task Restore_Visible_Conflict()
    {
        var response = await Decide(_api.CleanReviewId, "Restore", null);

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "NOT_HIDDEN");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #07] An unknown review id returns 404")]
    public async Task Decide_UnknownReview_NotFound()
    {
        var response = await Decide(999_999, "Hide", "Spam");

        await ApiAssert.Problem(response, HttpStatusCode.NotFound, "REVIEW_NOT_FOUND");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-CM-02 #08] A RetailCustomer cannot open the moderation queue")]
    public async Task List_RetailCustomer_Forbidden()
    {
        var response = await _api.Customer().GetAsync("api/moderation/reviews");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
