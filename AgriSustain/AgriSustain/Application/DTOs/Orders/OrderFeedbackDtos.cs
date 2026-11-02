namespace AgriSustain.Application.DTOs.Orders;

public sealed record OrderFeedbackContext(
    long OrderId, string OrderCode, string Status, DateTime OrderDate, decimal Total,
    string RecipientName, string RecipientPhone, string Address,
    IReadOnlyList<OrderFeedbackItem> Items);

public sealed record OrderFeedbackItem(
    long Id, long ProductId, string Name, string Category, string FarmName,
    bool IsFarmVerified, string Unit, decimal Quantity, decimal Price, decimal Total,
    string? ImageUrl,
    string SellerOrderCode, DateTime? DeliveredAt, bool CanReview, bool CanComplain,
    bool CanEditReview, ExistingReview? Review);

public sealed record ExistingReview(int Rating, string? Comment);

public sealed record ReviewSubmission(int Rating, string? Comment);

public sealed record ComplaintSubmission(
    IReadOnlyList<long> OrderItemIds, string? Reason, string? Description);

public sealed record ComplaintSubmissionResponse(IReadOnlyList<long> Ids, string Message);

public sealed record FeedbackSubmissionResponse(long Id, string Message);

public sealed record FeedbackFile(string FileName, long Length, Stream Content);

public sealed record FeedbackDownload(Stream Content, string ContentType, string FileName);
