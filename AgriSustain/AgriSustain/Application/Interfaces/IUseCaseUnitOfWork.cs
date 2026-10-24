namespace AgriSustain.Application.Interfaces;

public interface IUseCaseUnitOfWork
{
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
