using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Validators;

public static class OrderFeedbackValidator
{
    public static bool IsValid(ReviewSubmission request) =>
        request.Rating is >= 1 and <= 5 && IsTextValid(request.Comment, 1000);

    public static bool IsValid(ComplaintSubmission request) =>
        request.OrderItemIds is { Count: > 0 }
        && request.OrderItemIds.All(id => id > 0)
        && request.OrderItemIds.Distinct().Count() == request.OrderItemIds.Count
        && request.Reason is "NotAsDescribed" or "Damaged" or "MissingItems" or "Other"
        && IsTextValid(request.Description, 500);

    public static bool AreFilesValid(IReadOnlyList<FeedbackFile> files) =>
        files.Count <= 5 && files.All(file => file.Length is > 0 and <= 5 * 1024 * 1024);

    private static bool IsTextValid(string? text, int maximumLength) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximumLength
        && !text.Any(character => char.IsControl(character)
            && character is not ('\r' or '\n' or '\t'));
}
