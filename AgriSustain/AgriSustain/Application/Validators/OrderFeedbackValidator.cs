using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Validators;

public static class OrderFeedbackValidator
{
    public const int MaximumAttachments = 5;
    public const int MaximumImageBytes = 5 * 1024 * 1024;
    public const int MaximumVideoBytes = 20 * 1024 * 1024;
    public const string VideoContentType = "video/mp4";
    public const string EvidenceLimitsMessage =
        "Vui lòng đính kèm từ 1 đến 4 ảnh JPG/PNG (tối đa 5 MB mỗi ảnh) và 1 video MP4 (tối đa 20 MB).";

    public static bool IsValid(ReviewSubmission request) =>
        request.Rating is >= 1 and <= 5 && IsTextValid(request.Comment, 1000);

    public static bool IsValid(ComplaintSubmission request) =>
        request.OrderItemIds is { Count: > 0 }
        && request.OrderItemIds.All(id => id > 0)
        && request.OrderItemIds.Distinct().Count() == request.OrderItemIds.Count
        && request.Reason is "NotAsDescribed" or "Damaged" or "MissingItems" or "Other"
        && IsTextValid(request.Description, 500);

    public static bool AreFilesValid(IReadOnlyList<FeedbackFile> files) =>
        files.Count is >= 2 and <= MaximumAttachments
        && files.All(file => IsFileValid(file.FileName, file.Length))
        && files.Count(file => EvidenceContentType(file.FileName) == VideoContentType) == 1;

    public static bool IsFileValid(string name, long length) =>
        EvidenceContentType(name) is { } type && length > 0 && length <= MaximumFileBytes(type);

    public static int MaximumFileBytes(string contentType) =>
        contentType == VideoContentType ? MaximumVideoBytes : MaximumImageBytes;

    public static string? EvidenceContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".mp4" => VideoContentType,
        _ => null
    };

    private static bool IsTextValid(string? text, int maximumLength) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximumLength
        && !text.Any(character => char.IsControl(character)
            && character is not ('\r' or '\n' or '\t'));
}
