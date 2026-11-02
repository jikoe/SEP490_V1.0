using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces;

public interface IOrderFeedbackService
{
    Task<Result<OrderFeedbackContext>> GetContextAsync(
        long userId, long orderId, CancellationToken cancellationToken);

    Task<Result<FeedbackSubmissionResponse>> ReviewAsync(
        long userId, long orderId, long itemId, ReviewSubmission request,
        CancellationToken cancellationToken);

    Task<Result<FeedbackSubmissionResponse>> UpdateReviewAsync(
        long userId, long orderId, long itemId, ReviewSubmission request,
        CancellationToken cancellationToken);

    Task<Result<ComplaintSubmissionResponse>> ComplainAsync(
        long userId, long orderId, ComplaintSubmission request,
        IReadOnlyList<FeedbackFile> files, CancellationToken cancellationToken);
}
