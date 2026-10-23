using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;

namespace AgriSustain.Application.Interfaces.Services;

public interface IAccountStatusService
{
    /// <summary>
    /// Validates paging/filter input, requires an active SystemAdmin, and returns only the
    /// permitted account read model. No passwords or authentication secrets are exposed.
    /// </summary>
    Task<Result<PagedResponse<UserResponse>>> SearchAsync(
        UserQuery query, CancellationToken cancellationToken);
    /// <summary>
    /// Loads one permitted account by identity; reports not found without fabricating an empty
    /// account. Lock status is evaluated at the server time.
    /// </summary>
    Task<Result<UserResponse>> GetAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Checks confirmation, reason, target state and the self-action restriction, then saves the
    /// transition and before/after audit atomically. There is no SQL Version contract after DB-04
    /// was declined.
    /// </summary>
    Task<Result<UserResponse>> ChangeAsync(
        long id, AccountActionRequest request, CancellationToken cancellationToken);
}
