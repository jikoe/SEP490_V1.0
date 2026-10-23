namespace AgriSustain.Application.Interfaces;

public interface IFileStorageService
{
    bool OwnsImage(long userId, string url);
    bool OwnsEvidence(long userId, string url);
    Task<string> SaveImageAsync(Stream content, string extension,
        CancellationToken cancellationToken);
}


