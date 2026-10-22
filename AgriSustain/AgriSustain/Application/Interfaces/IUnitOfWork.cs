namespace AgriSustain.Application.Interfaces;

public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}


