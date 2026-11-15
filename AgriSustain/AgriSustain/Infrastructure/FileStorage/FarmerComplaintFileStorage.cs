using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed partial class FarmerComplaintFileStorage(IWebHostEnvironment environment)
    : IFarmerComplaintFiles
{
    public Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct) =>
        SaveCoreAsync(actor, file, allowVideo: false, ct);

    public Task<string> SaveResponseEvidenceAsync(
        long actor,
        IFormFile file,
        CancellationToken ct
    ) => SaveCoreAsync(actor, file, allowVideo: true, ct);

    private static int MaximumBytes(string name, bool allowVideo) =>
        !allowVideo && Path.GetExtension(name).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            ? 0
            : FarmerComplaintEvidenceRules.MaximumBytes(name);

    private async Task<string> SaveCoreAsync(
        long actor,
        IFormFile file,
        bool allowVideo,
        CancellationToken ct
    )
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        int maximumBytes = MaximumBytes(file.FileName, allowVideo);
        if (maximumBytes == 0 || file.Length <= 0 || file.Length > maximumBytes)
            throw new InvalidDataException(FarmerComplaintMessages.Invalid);
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
                throw new InvalidDataException(FarmerComplaintMessages.Invalid);
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        byte[] bytes = buffer.ToArray();
        if (bytes.Length != file.Length || !ValidHeader(bytes, extension))
            throw new InvalidDataException(FarmerComplaintMessages.Invalid);
        string key = Guid.NewGuid().ToString("N") + extension;
        string directory = Path.Combine(
            environment.ContentRootPath,
            "private-files",
            actor.ToString()
        );
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, key);
        try
        {
            await File.WriteAllBytesAsync(path, bytes, ct);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
        return $"/api/files/evidence/{actor}/{key}";
    }

    public string? Resolve(long actor, string url)
    {
        string? path =
            OrderFeedbackStorage.ResolveUrl(environment.ContentRootPath, actor, url)
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
        return Path.Combine(
            environment.ContentRootPath,
            "private-files",
            actor.ToString(),
            match.Groups["key"].Value
        );
    }

    public Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct) =>
        IsValidOwnedCoreAsync(actor, url, allowVideo: false, ct);

    public Task<bool> IsValidResponseEvidenceAsync(long actor, string url, CancellationToken ct) =>
        IsValidOwnedCoreAsync(actor, url, allowVideo: true, ct);

    private async Task<bool> IsValidOwnedCoreAsync(
        long actor,
        string url,
        bool allowVideo,
        CancellationToken ct
    )
    {
        string? path = ResolveResponseEvidence(actor, url);
        int maximumBytes = MaximumBytes(url, allowVideo);
        if (
            path is null
            || !File.Exists(path)
            || maximumBytes == 0
            || new FileInfo(path).Length <= 0
            || new FileInfo(path).Length > maximumBytes
        )
        {
            return false;
        }
        byte[] bytes = await File.ReadAllBytesAsync(path, ct);
        return ValidHeader(bytes, Path.GetExtension(path));
    }

    private static bool ValidHeader(byte[] bytes, string extension) =>
        extension switch
        {
            ".png" => bytes.Length >= 24
                && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".pdf" => bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-",
            ".jpg" or ".jpeg" => bytes.Length >= 4
                && bytes[0] == 255
                && bytes[1] == 216
                && bytes[2] == 255
                && bytes[^2] == 255
                && bytes[^1] == 217,
            ".mp4" => HasMp4Header(bytes),
            _ => false,
        };

    private static bool HasMp4Header(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
            return false;

        uint boxSize = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return boxSize >= 16
            && boxSize % 4 == 0
            && boxSize <= bytes.Length - 8
            && Encoding.ASCII.GetString(bytes.Slice(8, 4))
                is "isom"
                    or "iso2"
                    or "iso3"
                    or "iso4"
                    or "iso5"
                    or "iso6"
                    or "mp41"
                    or "mp42"
                    or "avc1"
                    or "M4V "
                    or "MSNV"
                    or "dash";
    }

    [GeneratedRegex(
        @"^/api/files/evidence/(?<owner>\d+)/(?<key>[0-9a-f]{32}\.(jpg|jpeg|png|pdf|mp4))$"
    )]
    private static partial Regex EvidencePath();
}
