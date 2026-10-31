using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class OrderFeedbackService(
    IOrderFeedbackRepository repository, IOrderFeedbackStorage storage,
    IUseCaseUnitOfWork unitOfWork, IPersistenceErrorClassifier errors,
    TimeProvider clock, ILogger<OrderFeedbackService> logger) : IOrderFeedbackService
{
    public async Task<Result<OrderFeedbackContext>> GetContextAsync(
        long userId, long orderId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOwnedOrderAsync(userId, orderId, cancellationToken);
        if (order == null)
        {
            return Result<OrderFeedbackContext>.FailureResult(new Error(
                "ORDER_NOT_FOUND", "Không tìm thấy đơn hàng của bạn.", "NotFound"));
        }

        var items = order.SellerOrders.SelectMany(seller => seller.OrderItems).ToArray();
        var images = await repository.GetImagesAsync(
            items.Select(item => item.ProductId).Distinct().ToArray(), cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        return Result<OrderFeedbackContext>.SuccessResult(new OrderFeedbackContext(
            order.OrderId, order.OrderCode, order.OrderStatus, order.OrderDate, order.FinalAmount,
            order.RecipientNameSnapshot, order.RecipientPhoneSnapshot, order.ShippingAddress,
            items.Select(item => new OrderFeedbackItem(
                item.OrderItemId, item.ProductId,
                item.ProductNameSnapshot ?? item.Product.ProductName,
                item.Product.Category.CategoryName, item.SellerOrder.SellerNameSnapshot
                    ?? item.SellerOrder.FarmerProfile.FarmName,
                item.SellerOrder.FarmerProfile.VerificationStatus is "Approved" or "Verified",
                item.UnitSnapshot, item.Quantity, item.UnitPriceSnapshot, item.SubTotal,
                images.GetValueOrDefault(item.ProductId), item.SellerOrder.SellerOrderCode,
                OrderFeedbackRules.DeliveredAt(item.SellerOrder),
                OrderFeedbackRules.CanReview(order, item),
                OrderFeedbackRules.CanComplain(order, item, now),
                OrderFeedbackRules.CanEditReview(order, item, userId),
                item.ProductReview is { } review && review.UserId == userId
                    ? new ExistingReview(review.RatingStars, review.Comment)
                    : null)).ToArray()));
    }

    public Task<Result<FeedbackSubmissionResponse>> ReviewAsync(
        long userId, long orderId, long itemId, ReviewSubmission request,
        CancellationToken cancellationToken) =>
        SaveReviewAsync(userId, orderId, itemId, request, false, cancellationToken);

    public Task<Result<FeedbackSubmissionResponse>> UpdateReviewAsync(
        long userId, long orderId, long itemId, ReviewSubmission request,
        CancellationToken cancellationToken) =>
        SaveReviewAsync(userId, orderId, itemId, request, true, cancellationToken);

    private async Task<Result<FeedbackSubmissionResponse>> SaveReviewAsync(
        long userId, long orderId, long itemId, ReviewSubmission request,
        bool editing, CancellationToken cancellationToken)
    {
        if (!OrderFeedbackValidator.IsValid(request))
        {
            return Failure("INVALID_REVIEW",
                "Vui lòng chọn từ 1 đến 5 sao và nhập nhận xét tối đa 1.000 ký tự.");
        }
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            var order = await repository.GetOwnedOrderAsync(userId, orderId, cancellationToken);
            var item = order?.SellerOrders.SelectMany(seller => seller.OrderItems)
                .SingleOrDefault(item => item.OrderItemId == itemId);
            if (order == null || item == null || !(editing
                ? OrderFeedbackRules.CanEditReview(order, item, userId)
                : OrderFeedbackRules.CanReview(order, item)))
            {
                return Failure("REVIEW_NOT_ELIGIBLE",
                    editing ? "Không thể sửa đánh giá của sản phẩm này. Vui lòng tải lại đơn hàng."
                        : "Sản phẩm chưa đủ điều kiện đánh giá hoặc đã được bạn đánh giá.",
                    "Conflict");
            }

            var review = editing ? item.ProductReview! : new ProductReview
            {
                OrderItemId = itemId,
                UserId = userId,
                ReviewDate = clock.GetUtcNow().UtcDateTime,
                IsPurchasedVerified = true
            };
            review.RatingStars = (byte)request.Rating;
            review.Comment = request.Comment!.Trim();
            review.ModerationStatus = "Approved";
            review.ModerationReason = null;
            if (!editing)
            {
                repository.Add(review);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<FeedbackSubmissionResponse>.SuccessResult(new(
                review.ProductReviewId, editing ? "Đánh giá của bạn đã được cập nhật."
                    : "Đánh giá của bạn đã được gửi thành công."));
        }
        catch (Exception exception) when (errors.IsConflict(exception))
        {
            return Failure("REVIEW_CONFLICT",
                "Dữ liệu đã thay đổi hoặc sản phẩm đã được đánh giá. Vui lòng tải lại.",
                "Conflict");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Could not save review for order item {OrderItemId}", itemId);
            return Failure("REVIEW_SAVE_FAILED",
                "Không thể lưu đánh giá. Vui lòng thử lại sau.", "Error");
        }
    }

    public async Task<Result<ComplaintSubmissionResponse>> ComplainAsync(
        long userId, long orderId, ComplaintSubmission request,
        IReadOnlyList<FeedbackFile> files, CancellationToken cancellationToken)
    {
        if (!OrderFeedbackValidator.IsValid(request))
        {
            return ComplaintFailure("INVALID_COMPLAINT",
                "Vui lòng chọn ít nhất một sản phẩm, lý do và nhập mô tả tối đa 500 ký tự.");
        }
        if (!OrderFeedbackValidator.AreFilesValid(files))
        {
            return ComplaintFailure("INVALID_EVIDENCE",
                "Chỉ được đính kèm tối đa 5 ảnh, mỗi ảnh không quá 5 MB.");
        }

        var saved = new List<string>();
        bool committed = false;
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            var order = await repository.GetOwnedOrderAsync(userId, orderId, cancellationToken);
            var requestedIds = request.OrderItemIds.ToHashSet();
            var items = order?.SellerOrders.SelectMany(seller => seller.OrderItems)
                .Where(item => requestedIds.Contains(item.OrderItemId)).ToArray() ?? [];
            var now = clock.GetUtcNow().UtcDateTime;
            if (order == null || items.Length != requestedIds.Count
                || items.Any(item => !OrderFeedbackRules.CanComplain(order, item, now)))
            {
                return ComplaintFailure("COMPLAINT_NOT_ELIGIBLE",
                    "Một hoặc nhiều sản phẩm không đủ điều kiện khiếu nại, đã có yêu cầu đang xử lý "
                        + "hoặc đã quá 48 giờ sau giao. Vui lòng tải lại đơn hàng.",
                    "Conflict");
            }

            foreach (var file in files)
            {
                saved.Add(await storage.SaveAsync(userId, file, cancellationToken));
            }
            var complaints = new List<DisputeTicket>();
            foreach (var sellerItems in items.GroupBy(item => item.SellerOrderId))
            {
                var complaint = new DisputeTicket
                {
                    SubmittedByUserId = userId,
                    SellerOrderId = sellerItems.Key,
                    DisputeReason = request.Reason!,
                    Description = OrderFeedbackRules.DescribeComplaint(sellerItems, request.Description!),
                    TicketStatus = "Open",
                    CreatedAt = now
                };
                foreach (string url in saved)
                {
                    complaint.DisputeEvidences.Add(new DisputeEvidence
                    {
                        SubmittedByUserId = userId,
                        FileUrl = url,
                        SubmittedAt = now
                    });
                }
                repository.Add(complaint);
                complaints.Add(complaint);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return Result<ComplaintSubmissionResponse>.SuccessResult(new(
                complaints.Select(complaint => complaint.DisputeTicketId).ToArray(),
                "Khiếu nại cho các sản phẩm đã chọn đã được gửi. Chúng tôi sẽ xem xét và phản hồi."));
        }
        catch (InvalidDataException exception)
        {
            return ComplaintFailure("INVALID_EVIDENCE", exception.Message);
        }
        catch (Exception exception) when (errors.IsConflict(exception))
        {
            return ComplaintFailure("COMPLAINT_CONFLICT",
                "Dữ liệu đã thay đổi. Vui lòng tải lại trước khi gửi.", "Conflict");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not save complaint for order {OrderId}", orderId);
            return ComplaintFailure("COMPLAINT_SAVE_FAILED",
                "Không thể gửi khiếu nại. Vui lòng thử lại sau.", "Error");
        }
        finally
        {
            if (!committed)
            {
                storage.Delete(userId, saved);
            }
        }
    }

    private static Result<ComplaintSubmissionResponse> ComplaintFailure(
        string code, string message, string kind = "Validation") =>
        Result<ComplaintSubmissionResponse>.FailureResult(new Error(code, message, kind));

    private static Result<FeedbackSubmissionResponse> Failure(
        string code, string message, string kind = "Validation") =>
        Result<FeedbackSubmissionResponse>.FailureResult(new Error(code, message, kind));
}
