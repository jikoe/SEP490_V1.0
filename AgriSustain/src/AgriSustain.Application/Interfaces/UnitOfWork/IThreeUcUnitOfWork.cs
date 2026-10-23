using AgriSustain.Application.Common;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

public interface IThreeUcUnitOfWork
{
    /// <summary>
    /// Runs the callback against one consistent session without publishing mutations.
    /// Cancellation is propagated to the operation.
    /// </summary>
    Task<Result<T>> ReadAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken);
    /// <summary>
    /// Runs the callback with one shared commit boundary: publish only on a successful Result,
    /// otherwise discard all staged changes, including audit and reservations.
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken);
}
