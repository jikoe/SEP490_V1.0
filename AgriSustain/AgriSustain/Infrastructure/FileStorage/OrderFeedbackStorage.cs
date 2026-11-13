using System.Buffers.Binary;
using System.Text;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed class OrderFeedbackStorage(
    IWebHostEnvironment environment, ILogger<OrderFeedbackStorage> logger) : IOrderFeedbackStorage
{
    public async Task<string> SaveAsync(long userId, FeedbackFile file,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string? contentType = OrderFeedbackValidator.EvidenceContentType(file.FileName);
        if (contentType is null || !OrderFeedbackValidator.IsFileValid(file.FileName, file.Length))
        {
            throw new InvalidDataException(OrderFeedbackValidator.EvidenceLimitsMessage);
        }
        int maximumBytes = OrderFeedbackValidator.MaximumFileBytes(contentType);
        var header = new byte[24];
        int read = await file.Content.ReadAtLeastAsync(header, header.Length, false, cancellationToken);
        bool validHeader = extension switch
        {
            ".png" => read == header.Length && header.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".mp4" => HasMp4Header(header.AsSpan(0, read), file.Length),
            _ => read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255
        };
        if (!validHeader)
        {
            throw new InvalidDataException("Nội dung tệp không đúng định dạng JPG, PNG hoặc MP4.");
        }

        string directory = DirectoryFor(environment.ContentRootPath, userId);
        Directory.CreateDirectory(directory);
        string key = Guid.NewGuid().ToString("N") + extension;
        string path = Path.Combine(directory, key);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew);
            await output.WriteAsync(header.AsMemory(0, read), cancellationToken);
            var buffer = new byte[81920];
            int total = read;
            while ((read = await file.Content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > maximumBytes)
                {
                    throw new InvalidDataException(OrderFeedbackValidator.EvidenceLimitsMessage);
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            return Prefix(userId) + key;
        }
        catch
        {
            DeletePath(path);
            throw;
        }
    }

    private static bool HasMp4Header(ReadOnlySpan<byte> header, long length)
    {
        if (header.Length < 16 || !header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }
        uint boxSize = BinaryPrimitives.ReadUInt32BigEndian(header);
        return boxSize >= 16 && boxSize % 4 == 0 && boxSize <= length - 8
            && Encoding.ASCII.GetString(header.Slice(8, 4)) is
                "isom" or "iso2" or "iso3" or "iso4" or "iso5" or "iso6"
                or "mp41" or "mp42" or "avc1" or "M4V " or "MSNV" or "dash";
    }

    public void Delete(long userId, IEnumerable<string> urls)
    {
        foreach (string url in urls)
        {
            string? path = ResolveUrl(environment.ContentRootPath, userId, url);
            if (path != null)
            {
                DeletePath(path);
            }
        }
    }

    public FeedbackDownload? OpenEvidence(long userId, string key)
    {
        if (!IsKeyValid(key))
        {
            return null;
        }
        string path = Path.Combine(DirectoryFor(environment.ContentRootPath, userId), key);
        try
        {
            return new FeedbackDownload(new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read), OrderFeedbackValidator.EvidenceContentType(key)!, key);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    internal static string? ResolveUrl(string root, long userId, string url)
    {
        string prefix = Prefix(userId);
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }
        string key = url[prefix.Length..];
        return IsKeyValid(key) ? Path.Combine(DirectoryFor(root, userId), key) : null;
    }

    private static string DirectoryFor(string root, long userId) => Path.Combine(
        root, "private-files/feedback",
        userId.ToString());

    private static string Prefix(long userId) => $"/api/orders/feedback-files/{userId}/";

    private static bool IsKeyValid(string key) => key == Path.GetFileName(key)
        && Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _)
        && OrderFeedbackValidator.EvidenceContentType(key) != null;

    private void DeletePath(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not remove uncommitted feedback attachment");
        }
    }
}
