using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Moderation;

// UC-CM-02 Moderate Product Review.

public static class ReviewTabs
{
    public const string Unreviewed = "Unreviewed";
    public const string Flagged = "Flagged";
    public const string Visible = "Visible";
    public const string Hidden = "Hidden";
    public static readonly string[] All = [Unreviewed, Flagged, Visible, Hidden];
}

public static class ReviewStatuses
{
    /// <summary>Visible on the product page (MarketplaceRules.PublicReview).</summary>
    public const string Visible = "Approved";
    public const string Hidden = "Hidden";
}

public static class ReviewDecisions
{
    public const string Hide = "Hide";
    public const string KeepVisible = "KeepVisible";
    public const string Restore = "Restore";
}

/// <summary>Raw review row read from the database.</summary>
public sealed record ReviewRecord(
    long ProductReviewId,
    long UserId,
    string ReviewerName,
    long ProductId,
    string ProductName,
    string OrderCode,
    byte RatingStars,
    string? Comment,
    DateTime ReviewDate,
    string ModerationStatus,
    string? ModerationReason,
    bool IsReviewed);

public sealed record ReviewListItemDto(
    long ProductReviewId,
    string ProductName,
    string ReviewerName,
    byte RatingStars,
    string? Comment,
    DateTime ReviewDate,
    string ModerationStatus,
    bool IsReviewed,
    IReadOnlyList<string> FlagReasons);

public sealed record ReviewDetailDto(
    long ProductReviewId,
    string ReviewerName,
    string ProductName,
    long ProductId,
    string OrderCode,
    byte RatingStars,
    string? Comment,
    DateTime ReviewDate,
    string ModerationStatus,
    string? ModerationReason,
    bool IsReviewed,
    IReadOnlyList<string> FlagReasons);

public sealed record ReviewQuery(string? Tab, string? Search, int Page = 1, int PageSize = 10);

public sealed record ReviewTabCounts(int Unreviewed, int Flagged, int Visible, int Hidden);

public sealed record ReviewListDto(
    IReadOnlyList<ReviewListItemDto> Items, int Total, int Page, int PageSize, ReviewTabCounts Counts);

public sealed record ModerateReviewRequest(string Decision, string? Reason);

public interface IReviewModerationRepository
{
    Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken cancellationToken);

    Task<ReviewRecord?> FindAsync(long reviewId, CancellationToken cancellationToken);

    /// <summary>Review dates of the given authors, used by the spam rule.</summary>
    Task<IReadOnlyList<(long UserId, DateTime ReviewDate)>> ReviewDatesAsync(
        IReadOnlyCollection<long> userIds, CancellationToken cancellationToken);

    /// <summary>Sets the moderation status/reason and writes the audit entry that marks the review as reviewed.</summary>
    Task ApplyDecisionAsync(long reviewId, string decision, string newStatus, string? reason,
        long moderatorUserId, DateTime now, CancellationToken cancellationToken);
}

/// <summary>Automatic flag rules applied when a review is listed (UC-CM-02 Step 2).</summary>
public static partial class ReviewFlagRules
{
    public const int SpamReviewLimit = 5;
    public static readonly TimeSpan SpamWindow = TimeSpan.FromHours(24);

    private static readonly string[] s_bannedWords =
    [
        "đm", "dm", "vcl", "vkl", "vl", "cc", "clm", "đéo", "địt", "lồn", "cặc", "ngu như",
        "fuck", "shit", "scam"
    ];

    public static IReadOnlyList<string> Evaluate(
        byte ratingStars, string? comment, int authorReviewsInWindow)
    {
        var reasons = new List<string>();
        string text = comment?.Trim() ?? string.Empty;
        if (text.Length > 0)
        {
            string lower = text.ToLowerInvariant();
            if (s_bannedWords.Any(word => WordPattern(word).IsMatch(lower)))
                reasons.Add("Chứa từ ngữ cấm");
            if (PhonePattern().IsMatch(text))
                reasons.Add("Chứa số điện thoại");
            if (LinkPattern().IsMatch(lower))
                reasons.Add("Chứa đường link");
            if (ratingStars == 1)
                reasons.Add("1 sao kèm bình luận");
        }

        if (authorReviewsInWindow > SpamReviewLimit)
            reasons.Add($"Người viết đăng hơn {SpamReviewLimit} đánh giá trong 24 giờ");
        return reasons;
    }

    /// <summary>Number of reviews the author posted in the 24 hours up to <paramref name="reviewDate"/>.</summary>
    public static int CountInWindow(IEnumerable<DateTime> authorDates, DateTime reviewDate) =>
        authorDates.Count(d => d <= reviewDate && reviewDate - d < SpamWindow);

    private static Regex WordPattern(string word) =>
        new($@"(^|[^\p{{L}}\p{{N}}]){Regex.Escape(word)}($|[^\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"(\+84|0)[\s.\-]?\d{2,3}[\s.\-]?\d{3}[\s.\-]?\d{3,4}")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"https?://|www\.|\b[a-z0-9-]+\.(com|vn|net|org|xyz|info|me|io)\b")]
    private static partial Regex LinkPattern();
}

public interface IReviewModerationService
{
    Task<Result<ReviewListDto>> ListAsync(ReviewQuery query, CancellationToken cancellationToken);

    Task<Result<ReviewDetailDto>> GetAsync(long reviewId, CancellationToken cancellationToken);

    Task<Result<ReviewDetailDto>> ModerateAsync(long reviewId, ModerateReviewRequest request,
        CancellationToken cancellationToken);
}

public sealed class ReviewModerationService(
    IReviewModerationRepository repository,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IReviewModerationService
{
    public const int MaxReasonLength = 500;

    // AF4
    public const string EmptyListMessage = "Không có đánh giá cần kiểm duyệt.";

    public async Task<Result<ReviewListDto>> ListAsync(ReviewQuery query, CancellationToken cancellationToken)
    {
        string tab = ReviewTabs.All.Contains(query.Tab) ? query.Tab! : ReviewTabs.Unreviewed;
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 50);

        var rows = await repository.ListAsync(cancellationToken);
        var evaluated = await EvaluateAsync(rows, cancellationToken);

        bool InTab(ReviewListItemDto r, string t) => t switch
        {
            ReviewTabs.Unreviewed => !r.IsReviewed && r.ModerationStatus != ReviewStatuses.Hidden,
            ReviewTabs.Flagged => !r.IsReviewed && r.ModerationStatus != ReviewStatuses.Hidden && r.FlagReasons.Count > 0,
            ReviewTabs.Hidden => r.ModerationStatus == ReviewStatuses.Hidden,
            _ => r.ModerationStatus == ReviewStatuses.Visible
        };

        var counts = new ReviewTabCounts(
            evaluated.Count(r => InTab(r, ReviewTabs.Unreviewed)),
            evaluated.Count(r => InTab(r, ReviewTabs.Flagged)),
            evaluated.Count(r => InTab(r, ReviewTabs.Visible)),
            evaluated.Count(r => InTab(r, ReviewTabs.Hidden)));

        // AF1: search by product name or comment text.
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var filtered = evaluated.Where(r => InTab(r, tab))
            .Where(r => search is null
                || r.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (r.Comment?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            // Flagged reviews first, then the newest.
            .OrderByDescending(r => r.FlagReasons.Count > 0)
            .ThenByDescending(r => r.ReviewDate)
            .ToList();

        return Result<ReviewListDto>.SuccessResult(new ReviewListDto(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            filtered.Count, page, pageSize, counts));
    }

    public async Task<Result<ReviewDetailDto>> GetAsync(long reviewId, CancellationToken cancellationToken)
    {
        var row = await repository.FindAsync(reviewId, cancellationToken);
        return row is null
            ? Result<ReviewDetailDto>.FailureResult(NotFound(reviewId))
            : Result<ReviewDetailDto>.SuccessResult(await DetailAsync(row, cancellationToken));
    }

    public async Task<Result<ReviewDetailDto>> ModerateAsync(long reviewId, ModerateReviewRequest request,
        CancellationToken cancellationToken)
    {
        string decision = request.Decision?.Trim() ?? string.Empty;
        string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        var row = await repository.FindAsync(reviewId, cancellationToken);
        if (row is null) return Result<ReviewDetailDto>.FailureResult(NotFound(reviewId));

        string newStatus;
        switch (decision)
        {
            case ReviewDecisions.Hide:
                // AF3: hiding requires a reason.
                if (reason is null || reason.Length > MaxReasonLength)
                {
                    return Result<ReviewDetailDto>.FailureResult(new Error("REASON_REQUIRED",
                        "Vui lòng nhập lý do kiểm duyệt hợp lệ (tối đa 500 ký tự).", "Validation",
                        new Dictionary<string, string[]> { ["reason"] = ["Vui lòng nhập lý do kiểm duyệt hợp lệ (tối đa 500 ký tự)."] }));
                }
                if (row.ModerationStatus == ReviewStatuses.Hidden)
                {
                    return Result<ReviewDetailDto>.FailureResult(new Error("ALREADY_HIDDEN",
                        "Đánh giá này đã bị ẩn.", "Conflict"));
                }
                newStatus = ReviewStatuses.Hidden;
                break;
            case ReviewDecisions.KeepVisible:
                if (row.ModerationStatus == ReviewStatuses.Hidden)
                {
                    return Result<ReviewDetailDto>.FailureResult(new Error("USE_RESTORE",
                        "Đánh giá đang bị ẩn. Hãy dùng Khôi phục để hiển thị lại.", "Conflict"));
                }
                newStatus = ReviewStatuses.Visible;
                reason = reason?.Length > MaxReasonLength ? reason[..MaxReasonLength] : reason;
                break;
            case ReviewDecisions.Restore:
                // AF2: only a hidden review can be restored.
                if (row.ModerationStatus != ReviewStatuses.Hidden)
                {
                    return Result<ReviewDetailDto>.FailureResult(new Error("NOT_HIDDEN",
                        "Chỉ khôi phục được đánh giá đang bị ẩn.", "Conflict"));
                }
                newStatus = ReviewStatuses.Visible;
                reason = null;
                break;
            default:
                return Result<ReviewDetailDto>.FailureResult(new Error("INVALID_DECISION",
                    "Thao tác kiểm duyệt không hợp lệ.", "Validation"));
        }

        await repository.ApplyDecisionAsync(reviewId, decision, newStatus,
            decision == ReviewDecisions.KeepVisible ? row.ModerationReason : reason,
            currentActor.UserId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var updated = await repository.FindAsync(reviewId, cancellationToken);
        return Result<ReviewDetailDto>.SuccessResult(await DetailAsync(updated!, cancellationToken));
    }

    private async Task<List<ReviewListItemDto>> EvaluateAsync(
        IReadOnlyList<ReviewRecord> rows, CancellationToken cancellationToken)
    {
        var dates = await repository.ReviewDatesAsync(rows.Select(r => r.UserId).Distinct().ToList(), cancellationToken);
        var byUser = dates.GroupBy(d => d.UserId).ToDictionary(g => g.Key, g => g.Select(d => d.ReviewDate).ToList());
        return rows.Select(r => new ReviewListItemDto(r.ProductReviewId, r.ProductName, r.ReviewerName,
            r.RatingStars, r.Comment, r.ReviewDate, r.ModerationStatus, r.IsReviewed,
            ReviewFlagRules.Evaluate(r.RatingStars, r.Comment,
                ReviewFlagRules.CountInWindow(byUser.GetValueOrDefault(r.UserId) ?? [], r.ReviewDate))))
            .ToList();
    }

    private async Task<ReviewDetailDto> DetailAsync(ReviewRecord row, CancellationToken cancellationToken)
    {
        var item = (await EvaluateAsync([row], cancellationToken))[0];
        return new ReviewDetailDto(row.ProductReviewId, row.ReviewerName, row.ProductName, row.ProductId,
            row.OrderCode, row.RatingStars, row.Comment, row.ReviewDate, row.ModerationStatus,
            row.ModerationReason, row.IsReviewed, item.FlagReasons);
    }

    private static Error NotFound(long id) =>
        new("REVIEW_NOT_FOUND", $"Không tìm thấy đánh giá #{id}.", "NotFound");
}
