using System.Data;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriSustain.Infrastructure.UnitOfWork;

public sealed class SqlUnitOfWork(AgriSustainContext context) : IUseCaseUnitOfWork
{
    public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken)
    {
        return new SqlTransaction(await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed class SqlTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
