using AgriSustain.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data;

public sealed class PersistenceErrorClassifier : IPersistenceErrorClassifier
{
    public bool IsConflict(Exception exception)
    {
        return exception is DbUpdateConcurrencyException
            || exception is SqlException { Number: 1205 or 2601 or 2627 or 547 }
            || exception.InnerException is SqlException { Number: 1205 or 2601 or 2627 or 547 };
    }
}
