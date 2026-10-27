using AgriSustain.Application.Common;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork, AgriSustain.Application.Interfaces.IUnitOfWork, IAsyncDisposable
{
    private readonly AgriSustainContext _db;
    private IDbContextTransaction? _transaction;
    private readonly SqlDemoDependencies _demoStore;

    public IAuthRepository Auth { get; }
    public IProfileRepository Profiles { get; }
    public IOrderTrackingRepository Orders { get; }

    public UnitOfWork(AgriSustainContext db, SqlDemoDependencies demoStore)
    {
        _db = db;
        _demoStore = demoStore;
        Auth = new AuthRepository(db, demoStore);
        Profiles = new ProfileRepository(db);
        Orders = new OrderTrackingRepository(db);
    }

    public async Task<Result<T>> ReadAsync<T>(
        Func<IUnitOfWork, Task<Result<T>>> operation, CancellationToken cancellationToken = default) =>
        await operation(this);

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<IUnitOfWork, Task<Result<T>>> operation, CancellationToken cancellationToken = default)
    {
        await BeginAsync(cancellationToken);
        try
        {
            var result = await operation(this);

            if (result.IsSuccess)
            {
                await CommitAsync(cancellationToken);
            }
            else
            {
                await RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateException ex)
        {
            await RollbackAsync(cancellationToken);
            System.Diagnostics.Trace.TraceError(ex.ToString());
            return Result<T>.FailureResult(new Error("SAVE_CONFLICT",
                "Dữ liệu vừa bị thay đổi hoặc bị trùng. Vui lòng tải lại và thử lại.", "Conflict"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollbackAsync(cancellationToken);
            // Never return exception details (SQL, stack traces) to the client.
            System.Diagnostics.Trace.TraceError(ex.ToString());
            return Result<T>.FailureResult(new Error("SYSTEM_ERROR",
                "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.", "Error"));
        }
    }
    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        _transaction ??= await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        _db.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
