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
        string key = Guid.NewGuid().ToString("N") + extension;
        await using var output = new FileStream(Path.Combine(directory, key), FileMode.CreateNew);
        await content.CopyToAsync(output, cancellationToken);
        return $"/media/{actor.UserId}/{key}";
    }
}


