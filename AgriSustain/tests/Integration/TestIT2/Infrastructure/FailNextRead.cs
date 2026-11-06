using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgriSustain.TestIT2.Infrastructure;

internal sealed class FailNextRead : DbCommandInterceptor
{
    public bool Armed;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (Armed)
        {
            Armed = false;
            throw new IOException("Test-only retrieval failure");
        }

        return ValueTask.FromResult(result);
    }
}
