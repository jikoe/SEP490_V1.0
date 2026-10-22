using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.UnitOfWork;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AgriSustainContext _db;
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
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(this);

            if (result.IsSuccess)
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<T>.FailureResult(new Error("SYSTEM_ERROR", ex.Message, "Error"));
        }
    }
}
