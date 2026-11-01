namespace AgriSustain.Application.Interfaces.Repositories;

/// <summary>
/// Private storage of legal documents (Business License, certificate dossiers). Files are never
/// served from wwwroot; they are read through an authorized endpoint.
/// </summary>
public interface IDossierStorage
{
    /// <summary>Stores a document for an existing user and returns its private URL.</summary>
    Task<string> SaveAsync(Stream content, string extension, string folder, long ownerUserId,
        CancellationToken cancellationToken);
}
