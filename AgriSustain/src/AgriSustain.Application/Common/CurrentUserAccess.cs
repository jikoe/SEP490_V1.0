using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Common;

public static class CurrentUserAccess
{
    /// <summary>
    /// Resolves the actor from the trusted current-user abstraction and rechecks its role, active
    /// status and lock state inside the session; never trusts an actor ID supplied in request
    /// JSON.
    /// </summary>
    public static async Task<Result<UserAccount>> RequireAsync(
        IThreeUcSession session, ICurrentUser currentUser, string role, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        UserAccount? actor = currentUser.UserId is { } id
            ? await session.Accounts.FindAsync(id, cancellationToken) : null;
        if (actor == null)
        {
            return Result<UserAccount>.Failure(new Error(
                "UNAUTHENTICATED", "Vui lòng xác thực trước khi tiếp tục.", "Unauthorized"));
        }

        if (actor.Role != role || actor.Status != "Active" || actor.IsTemporarilyLocked(now))
        {
            return Result<UserAccount>.Failure(new Error(
                "FORBIDDEN", "Tài khoản không được phép thực hiện thao tác này.", "Forbidden"));
        }

        return Result<UserAccount>.Success(actor);
    }
}
