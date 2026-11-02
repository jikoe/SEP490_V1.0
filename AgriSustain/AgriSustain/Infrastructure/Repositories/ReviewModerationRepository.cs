using System.Text.Json;
using AgriSustain.Application.Services.Moderation;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>
/// UC-CM-02 persistence over dbo.ProductReview. A review counts as "reviewed by a moderator"
/// when an AuditLog entry with EntityType = ProductReview and Action = ModerateReview:* exists,
/// so no new column is needed.
/// </summary>
public sealed class ReviewModerationRepository(AgriSustainContext db) : IReviewModerationRepository
{
    private const string EntityType = "ProductReview";
    private const string ActionPrefix = "ModerateReview:";

    public async Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken cancellationToken) =>
        await Project(db.ProductReviews.AsNoTracking()).ToListAsync(cancellationToken);

    public Task<ReviewRecord?> FindAsync(long reviewId, CancellationToken cancellationToken) =>
        Project(db.ProductReviews.AsNoTracking().Where(r => r.ProductReviewId == reviewId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<(long UserId, DateTime ReviewDate)>> ReviewDatesAsync(
        IReadOnlyCollection<long> userIds, CancellationToken cancellationToken)
    {
        var rows = await db.ProductReviews.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId))
            .Select(r => new { r.UserId, r.ReviewDate })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.UserId, r.ReviewDate)).ToList();
    }

    public async Task ApplyDecisionAsync(long reviewId, string decision, string newStatus, string? reason,
        long moderatorUserId, DateTime now, CancellationToken cancellationToken)
    {
        var review = await db.ProductReviews.FirstAsync(r => r.ProductReviewId == reviewId, cancellationToken);
        var before = new { review.ModerationStatus, review.ModerationReason };
        review.ModerationStatus = newStatus;
        review.ModerationReason = reason;
        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = moderatorUserId,
            EntityType = EntityType,
            EntityId = reviewId,
            Action = ActionPrefix + decision,
            OldValue = JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(new { review.ModerationStatus, review.ModerationReason }),
            Reason = reason,
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ReviewRecord> Project(IQueryable<ProductReview> source) =>
        source.Select(r => new ReviewRecord(
            r.ProductReviewId,
            r.UserId,
            r.User.FullName,
            r.OrderItem.ProductId,
            r.OrderItem.Product.ProductName,
            r.OrderItem.SellerOrder.Order.OrderCode,
            r.RatingStars,
            r.Comment,
            r.ReviewDate,
            r.ModerationStatus,
            r.ModerationReason,
            db.AuditLogs.Any(a => a.EntityType == EntityType && a.EntityId == r.ProductReviewId
                && a.Action.StartsWith(ActionPrefix))));
}
