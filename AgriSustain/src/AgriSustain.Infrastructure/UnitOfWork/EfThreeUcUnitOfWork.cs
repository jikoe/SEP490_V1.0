using System.Data;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories.EfCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriSustain.Infrastructure.UnitOfWork;

/// <summary>
/// Commits EF business, audit and inventory changes together. External point/session/request-key
/// projections remain process-local mocks until their owning modules supply real adapters.
/// </summary>
public sealed class EfThreeUcUnitOfWork(AgriSustainDbContext db, ICurrentUser actor,
    SqlDemoDependencies mocks, TimeProvider clock) : IThreeUcUnitOfWork
{
    /// <summary>Runs a consistent read and rolls back any accidentally staged writes.</summary>
    public Task<Result<T>> ReadAsync<T>(Func<IThreeUcSession, Task<Result<T>>> operation,
        CancellationToken cancellationToken) => RunAsync(operation, false, cancellationToken);

    /// <summary>Commits a successful use case and publishes mock projections after SQL commits.</summary>
    public Task<Result<T>> ExecuteAsync<T>(Func<IThreeUcSession, Task<Result<T>>> operation,
        CancellationToken cancellationToken) => RunAsync(operation, true, cancellationToken);

    /// <summary>
    /// Owns the scoped EF transaction and serializes local mock changes. A transaction-owned SQL
    /// application lock also protects writes from independent review hosts; it is intentionally
    /// broad and must be revisited for production throughput by the infrastructure team.
    /// </summary>
    private async Task<Result<T>> RunAsync<T>(Func<IThreeUcSession, Task<Result<T>>> operation,
        bool commit, CancellationToken cancellationToken)
    {
        await mocks.Gate.WaitAsync(cancellationToken);
        try
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                if (commit)
                {
                    // Fixed SQL only for transaction coordination; business queries/CRUD use EF.
                    await db.Database.ExecuteSqlRawAsync("""
                        DECLARE @Result int;
                        EXEC @Result=sys.sp_getapplock @Resource='AgriSustain.ThreeUc.SqlDemo',
                            @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                        IF @Result<0 THROW 51140,'Review transaction lock unavailable.',1;
                        """, cancellationToken);
                }
                var session = new EfThreeUcSession(db, actor.UserId, clock, mocks);
                var result = await operation(session);
                cancellationToken.ThrowIfCancellationRequested();
                if (commit && result.IsSuccess)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    mocks.Publish(session.Points, session.RequestOrders, session.SessionVersions);
                }
                else
                {
                    await RollbackAsync(transaction);
                }
                return result;
            }
            catch (Exception exception) when (IsConflict(exception))
            {
                await RollbackAsync(transaction);
                return Result<T>.Failure(new Error("SQL_CONFLICT",
                    "Dữ liệu liên quan đã thay đổi hoặc vẫn còn tham chiếu. Vui lòng tải lại.",
                    "Conflict"));
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
            mocks.Gate.Release();
        }
    }

    /// <summary>Recognizes constraint/deadlock/concurrency conflicts without exposing SQL details.</summary>
    private static bool IsConflict(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || exception is SqlException { Number: 547 or 2601 or 2627 or 1205 or 51140 }
        || exception is DbUpdateException { InnerException: SqlException
            { Number: 547 or 2601 or 2627 or 1205 } };

    /// <summary>Rolls back unless SQL Server has already aborted and detached the transaction.</summary>
    private static Task RollbackAsync(IDbContextTransaction transaction) =>
        transaction.GetDbTransaction().Connection is null
            ? Task.CompletedTask : transaction.RollbackAsync(CancellationToken.None);
}
