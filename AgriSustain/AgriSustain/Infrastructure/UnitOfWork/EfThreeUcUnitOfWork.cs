using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories.EfCore;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.UnitOfWork;

public sealed class EfThreeUcUnitOfWork(AgriSustainContext db, ICurrentUser actor, SqlDemoDependencies dependencies) : IThreeUcUnitOfWork
{
    public async Task<Result<T>> ReadAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken)
    {
        var session = new EfThreeUcSession(db, actor.UserId, dependencies);
        return await operation(session);
    }

    public async Task<Result<T>> ExecuteAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var session = new EfThreeUcSession(db, actor.UserId, dependencies);
            var result = await operation(session);

            if (result.IsSuccess)
            {
                await db.SaveChangesAsync(cancellationToken);
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
