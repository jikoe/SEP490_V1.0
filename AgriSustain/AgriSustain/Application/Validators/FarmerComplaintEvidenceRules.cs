namespace AgriSustain.Application.Validators;

public static class FarmerComplaintEvidenceRules
{
    public const int MaximumFiles = 5;
    public const int MaximumVideos = 1;
    public const int MaximumImageOrDocumentBytes = 5 * 1024 * 1024;
    public const int MaximumVideoBytes = 20 * 1024 * 1024;
    public const int MaximumUploadRequestBytes = MaximumVideoBytes + 65536;

    public static int MaximumBytes(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".pdf" => MaximumImageOrDocumentBytes,
            ".mp4" => MaximumVideoBytes,
            _ => 0,
        };

    public static bool IsValidSelection(string[]? urls) =>
        urls is { Length: >= 1 and <= MaximumFiles }
        && urls.All(url => !string.IsNullOrWhiteSpace(url) && MaximumBytes(url) > 0)
        && urls.Distinct(StringComparer.Ordinal).Count() == urls.Length
        && urls.Count(url =>
            Path.GetExtension(url).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
        ) <= MaximumVideos;
}
