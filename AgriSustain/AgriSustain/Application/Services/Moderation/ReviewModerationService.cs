using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Moderation;

// UC-CM-02 Moderate Product Review.

/// <summary>
/// Post-moderation: a clean review is published at once and never enters the queue. The moderator only
/// works on reviews the automatic rules flagged (Flagged = "Cần xử lý") and can restore hidden ones.
/// </summary>
public static class ReviewTabs
{
    public const string Flagged = "Flagged";
    public const string Hidden = "Hidden";
    public static readonly string[] All = [Flagged, Hidden];
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

public sealed record ReviewQuery(string? Tab, int Page = 1, int PageSize = 10);

public sealed record ReviewTabCounts(int Flagged, int Hidden);

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

/// <summary>
/// Automatic flag rules (UC-CM-02 Step 2). Only offensive content is flagged: vulgar or obscene language,
/// regional discrimination, and reactionary / anti-state content. Low ratings, links or phone numbers are
/// not violations by themselves.
/// </summary>
public static class ReviewFlagRules
{
    public const string Vulgar = "Ngôn từ thô tục";
    public const string RegionalDiscrimination = "Phân biệt vùng miền";
    public const string Reactionary = "Dấu hiệu phản động";

    private static readonly string[] s_vulgar =
    [
        "đm", "dm", "đmm", "vcl", "vkl", "vl", "cc", "clm", "cl", "đéo", "địt", "lồn", "cặc", "buồi", "đĩ",
        "óc chó", "ngu như chó", "mẹ mày", "con mẹ", "fuck", "shit", "bitch"
    ];

    private static readonly string[] s_regional =
    [
        "bắc kỳ", "bắc kì", "nam kỳ", "nam kì", "trung kỳ", "trung kì", "parky", "nam cầy", "mọi rợ",
        "dân thanh hóa", "thanh hoá ăn trộm", "dân nghệ", "rau muống", "bò đỏ", "dân rau muống"
    ];

    private static readonly string[] s_reactionary =
    [
        "lật đổ", "phản động", "đả đảo", "chế độ thối nát", "cộng phỉ", "ba que", "3 que", "việt tân",
        "đảng cướp", "bọn cộng sản"
    ];

    public static IReadOnlyList<string> Evaluate(string? comment)
    {
        var reasons = new List<string>();
        string text = (comment ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return reasons;
        if (ContainsAny(text, s_vulgar)) reasons.Add(Vulgar);
        if (ContainsAny(text, s_regional)) reasons.Add(RegionalDiscrimination);
        if (ContainsAny(text, s_reactionary)) reasons.Add(Reactionary);
        return reasons;
    }

    private static bool ContainsAny(string text, string[] words) =>
        words.Any(word => Regex.IsMatch(text,
            $@"(^|[^\p{{L}}\p{{N}}]){Regex.Escape(word)}($|[^\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant));
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
        string tab = ReviewTabs.All.Contains(query.Tab) ? query.Tab! : ReviewTabs.Flagged;
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 50);

        var rows = await repository.ListAsync(cancellationToken);
        var evaluated = await EvaluateAsync(rows, cancellationToken);

        // Queue: flagged, still visible and not handled yet. Kept-visible reviews leave the queue.
        bool InTab(ReviewListItemDto r, string t) => t == ReviewTabs.Hidden
            ? r.ModerationStatus == ReviewStatuses.Hidden
            : !r.IsReviewed && r.ModerationStatus != ReviewStatuses.Hidden && r.FlagReasons.Count > 0;

        var counts = new ReviewTabCounts(
            evaluated.Count(r => InTab(r, ReviewTabs.Flagged)),
            evaluated.Count(r => InTab(r, ReviewTabs.Hidden)));

        // Oldest first in the queue so nothing waits forever; newest first among hidden ones.
        var inTab = evaluated.Where(r => InTab(r, tab));
        var filtered = (tab == ReviewTabs.Flagged
            ? inTab.OrderBy(r => r.ReviewDate)
            : inTab.OrderByDescending(r => r.ReviewDate)).ToList();

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

    private static Task<List<ReviewListItemDto>> EvaluateAsync(
        IReadOnlyList<ReviewRecord> rows, CancellationToken cancellationToken) =>
        Task.FromResult(rows.Select(r => new ReviewListItemDto(r.ProductReviewId, r.ProductName, r.ReviewerName,
            r.RatingStars, r.Comment, r.ReviewDate, r.ModerationStatus, r.IsReviewed,
            ReviewFlagRules.Evaluate(r.Comment))).ToList());

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
