using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

public interface IUnitOfWork
{
    IAuthRepository Auth { get; }
    IProfileRepository Profiles { get; }
    IOrderTrackingRepository Orders { get; }

    Task<Result<T>> ReadAsync<T>(
        Func<IUnitOfWork, Task<Result<T>>> operation, CancellationToken cancellationToken = default);

    Task<Result<T>> ExecuteAsync<T>(
        Func<IUnitOfWork, Task<Result<T>>> operation, CancellationToken cancellationToken = default);
}
