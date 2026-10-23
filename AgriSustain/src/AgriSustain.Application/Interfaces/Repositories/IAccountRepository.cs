using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IAccountRepository
{
    /// <summary>
    /// Loads the account by database UserId within the current session; null means it does not
    /// exist. Callers must still enforce actor/target authorization.
    /// </summary>
    Task<UserAccount?> FindAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Returns a stable, paged account projection for administrator filters. A SQL implementation
    /// must filter and page before materialization and omit secret columns.
    /// </summary>
    Task<PagedResponse<UserResponse>> SearchAsync(
        UserQuery query, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>
    /// Stages the account transition in the same transaction as its audit. Implementations must
    /// not commit independently or invent a Version column.
    /// </summary>
    Task SaveAsync(UserAccount account, CancellationToken cancellationToken);
}
