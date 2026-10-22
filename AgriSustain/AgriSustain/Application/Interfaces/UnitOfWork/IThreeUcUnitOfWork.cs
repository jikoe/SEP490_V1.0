using AgriSustain.Application.Common;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

public interface IThreeUcUnitOfWork
{
    Task<Result<T>> ReadAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken);

    Task<Result<T>> ExecuteAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken);
}
