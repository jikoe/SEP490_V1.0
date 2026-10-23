using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories.Demo;

namespace AgriSustain.Infrastructure.UnitOfWork;

// DEMO-PERSISTENCE: the user authorized simulations for missing dependencies.
// This gate + copy-on-write snapshot provides atomic rollback/concurrency inside ONE
// process only. It does not coordinate multiple servers and is not durable on restart.
// Replace with a scoped SQL UoW + conditional stock updates using the approved schema.
// DB-06 retry storage remains undecided; do not invent request-key columns in SQL.
// Never register this adapter in Production.
public sealed class DemoUnitOfWork :
    IThreeUcUnitOfWork, IDemoIdentityReader, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private DemoState _state;

    /// <summary>
    /// Creates the process-local store from synthetic fixtures or a supplied test seed. Copies
    /// the seed to keep the caller from mutating committed state.
    /// </summary>
    public DemoUnitOfWork(TimeProvider timeProvider)
        : this(timeProvider, DemoState.Seed(timeProvider.GetUtcNow()))
    {
    }

    /// <summary>
    /// Creates the process-local store from synthetic fixtures or a supplied test seed. Copies
    /// the seed to keep the caller from mutating committed state.
    /// </summary>
    internal DemoUnitOfWork(TimeProvider timeProvider, DemoState state)
    {
        _timeProvider = timeProvider;
        _state = state.Copy();
    }

    /// <summary>
    /// Returns a detached snapshot for assertions so a test cannot mutate committed fixture state
    /// through a returned entity reference.
    /// </summary>
    internal async Task<DemoState> SnapshotAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return _state.Copy();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs the callback against one consistent session without publishing mutations.
    /// Cancellation is propagated to the operation.
    /// </summary>
    public Task<Result<T>> ReadAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken) =>
        RunAsync(operation, false, cancellationToken);

    /// <summary>
    /// Runs the callback with one shared commit boundary: publish only on a successful Result,
    /// otherwise discard all staged changes, including audit and reservations.
    /// </summary>
    public Task<Result<T>> ExecuteAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken) =>
        RunAsync(operation, true, cancellationToken);

    /// <summary>
    /// Resolves a public fixture selector, rechecks active/lock state and accepts only its
    /// original mock session version. This is not credential or JWT validation.
    /// </summary>
    public async Task<(long Id, string Role)?> FindAsync(
        string fixture, CancellationToken cancellationToken)
    {
        // DEMO-AUTH: fixture names are public test selectors, NOT credentials or JWTs.
        // SessionVersion=0 represents the seeded session; suspension invalidates it even
        // after activation. Only restarting the isolated demo seeds fresh fixture sessions.
        long id = fixture switch
        {
            "admin" => 1,
            "support" => 13,
            "retail-customer" => 10,
            "other-retail-customer" => 11,
            _ => 0
        };
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var account = _state.Accounts.GetValueOrDefault(id);
            return account is { Status: "Active", SessionVersion: 0 }
                && !account.IsTemporarilyLocked(_timeProvider.GetUtcNow())
                ? (account.Id, account.Role) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Releases the semaphore owned by this demo store; DI controls when the singleton is
    /// disposed.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// Serializes access to a copied fixture snapshot and publishes another copy only after a
    /// successful commit. The semaphore protects one process only; SQL transactions are still
    /// required for real persistence.
    /// </summary>
    private async Task<Result<T>> RunAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, bool commit,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            DemoState working = _state.Copy();
            var result = await operation(new DemoSession(working));
            cancellationToken.ThrowIfCancellationRequested();
            if (commit && result.IsSuccess)
            {
                // Publish another copy: entities returned by a callback cannot mutate the store.
                _state = working.Copy();
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
