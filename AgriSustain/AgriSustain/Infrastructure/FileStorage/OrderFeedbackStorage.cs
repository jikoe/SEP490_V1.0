using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed class OrderFeedbackStorage(
    IWebHostEnvironment environment, ILogger<OrderFeedbackStorage> logger) : IOrderFeedbackStorage
{
    private const int MaximumBytes = 5 * 1024 * 1024;

    public async Task<string> SaveAsync(long userId, FeedbackFile file,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
        {
            throw new InvalidDataException("Vui lòng chọn ảnh JPG hoặc PNG.");
        }
        var header = new byte[24];
        int read = await file.Content.ReadAtLeastAsync(header, 24, false, cancellationToken);
        bool png = read == 24 && header.AsSpan(0, 8).SequenceEqual(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (extension == ".png" ? !png : !jpeg)
        {
            throw new InvalidDataException("Nội dung ảnh không đúng định dạng JPG hoặc PNG.");
        }

        string directory = DirectoryFor(userId);
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
                if (total > MaximumBytes)
                {
                    throw new InvalidDataException("Mỗi ảnh không được vượt quá 5 MB.");
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

    public void Delete(long userId, IEnumerable<string> urls)
    {
        foreach (string url in urls)
        {
            string prefix = Prefix(userId);
            if (url.StartsWith(prefix, StringComparison.Ordinal))
            {
                string key = url[prefix.Length..];
                if (IsKeyValid(key))
                {
                    DeletePath(Path.Combine(DirectoryFor(userId), key));
                }
            }
        }
    }

    public FeedbackDownload? OpenEvidence(long userId, string key)
    {
        if (!IsKeyValid(key))
        {
            return null;
        }
        string path = Path.Combine(DirectoryFor(userId), key);
        try
        {
            return new FeedbackDownload(new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read), Path.GetExtension(key) == ".png"
                    ? "image/png" : "image/jpeg", key);
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

    private string DirectoryFor(long userId) => Path.Combine(
        environment.ContentRootPath, "private-files/feedback",
        userId.ToString());

    private static string Prefix(long userId) => $"/api/orders/feedback-files/{userId}/";

    private static bool IsKeyValid(string key) => key == Path.GetFileName(key)
        && Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _)
        && Path.GetExtension(key) is ".jpg" or ".jpeg" or ".png";

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
