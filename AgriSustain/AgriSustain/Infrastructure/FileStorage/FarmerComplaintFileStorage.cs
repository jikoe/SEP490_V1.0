using System.Text;
using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed partial class FarmerComplaintFileStorage(IWebHostEnvironment environment) : IFarmerComplaintFiles
{
    private const int MaximumBytes = 5 * 1024 * 1024;

    public async Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length is <= 0 or > MaximumBytes || extension is not (".jpg" or ".jpeg" or ".png" or ".pdf"))
            throw new InvalidDataException(FarmerComplaintMessages.Invalid);
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        byte[] bytes = buffer.ToArray();
        if (bytes.Length > MaximumBytes || !ValidHeader(bytes, extension))
            throw new InvalidDataException(FarmerComplaintMessages.Invalid);
        string key = Guid.NewGuid().ToString("N") + extension;
        string directory = Path.Combine(environment.ContentRootPath, "private-files", actor.ToString());
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, key);
        try
        {
            await File.WriteAllBytesAsync(path, bytes, ct);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path); throw;
        }
        return $"/api/files/evidence/{actor}/{key}";
    }

    public string? Resolve(long actor, string url)
    {
        string? path = OrderFeedbackStorage.ResolveUrl(environment.ContentRootPath, actor, url)
            ?? ResolveResponseEvidence(actor, url);
        return File.Exists(path) ? path : null;
    }

    private string? ResolveResponseEvidence(long actor, string url)
    {
        var match = EvidencePath().Match(url);
        if (!match.Success || match.Groups["owner"].Value != actor.ToString())
        {
            return null;
        }
        return Path.Combine(environment.ContentRootPath, "private-files", actor.ToString(), match.Groups["key"].Value);
    }

    public async Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct)
    {
        string? path = ResolveResponseEvidence(actor, url);
        if (path is null || !File.Exists(path) || new FileInfo(path).Length is <= 0 or > MaximumBytes)
        {
            return false;
        }
        byte[] bytes = await File.ReadAllBytesAsync(path, ct);
        return ValidHeader(bytes, Path.GetExtension(path));
    }

    private static bool ValidHeader(byte[] bytes, string extension) => extension switch
    {
        ".png" => bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".pdf" => bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-",
        ".jpg" or ".jpeg" => bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217,
        _ => false
    };

    [GeneratedRegex(@"^/api/files/evidence/(?<owner>\d+)/(?<key>[0-9a-f]{32}\.(jpg|jpeg|png|pdf))$")]
    private static partial Regex EvidencePath();
}
