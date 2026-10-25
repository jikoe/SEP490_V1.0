namespace AgriSustain.Application.Interfaces;

/// <summary>Disposal without a successful commit rolls back the database transaction.</summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
