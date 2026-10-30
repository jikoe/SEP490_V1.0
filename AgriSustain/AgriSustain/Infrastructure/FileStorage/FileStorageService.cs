using AgriSustain.Application.Interfaces;

namespace AgriSustain.Infrastructure.FileStorage;

public sealed class FileStorageService(IWebHostEnvironment environment, ICurrentActor actor) : IFileStorageService
{
    public bool OwnsImage(long userId, string url) =>
        Owns(userId, url, $"/media/{userId}/", "wwwroot/media");

    public bool OwnsEvidence(long userId, string url) =>
        Owns(userId, url, $"/api/files/evidence/{userId}/", "private-files");

    private bool Owns(long userId, string url, string prefix, string directory)
    {
        if (!url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string key = url[prefix.Length..];
        return key == Path.GetFileName(key)
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _)
            && File.Exists(Path.Combine(environment.ContentRootPath, directory,
                userId.ToString(), key));
    }

    public async Task<string> SaveImageAsync(Stream content, string extension,
        CancellationToken cancellationToken)
    {
        string directory = Path.Combine(environment.ContentRootPath, "wwwroot", "media", actor.UserId.ToString());
        Directory.CreateDirectory(directory);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var hash = System.Security.Cryptography.SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        // Same content uploaded by the same owner has one reference, so duplicate gallery images fail validation.
        string key = Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant()
            + (extension == ".jpeg" ? ".jpg" : extension);
        string path = Path.Combine(directory, key);
        if (!File.Exists(path))
        {
            string temporary = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, buffer.ToArray(), cancellationToken);
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException) when (File.Exists(path)) { /* Identical content was stored concurrently. */ }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return $"/media/{actor.UserId}/{key}";
    }
}
