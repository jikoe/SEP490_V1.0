using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Mappings;
using AgriSustain.Domain.Entities.Accounts;
using FluentValidation;

namespace AgriSustain.Application.Services.Administration;

/// <summary>
/// Receives account persistence, actor identity, server clock and validators for UC-ADM-03.
/// Authentication remains an external adapter, not a login implementation.
/// </summary>
public sealed class AccountStatusService(
    IThreeUcUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider,
    IValidator<UserQuery> queryValidator, IValidator<AccountActionRequest> actionValidator)
    : IAccountStatusService
{
    /// <summary>
    /// Validates paging/filter input, requires an active SystemAdmin, and returns only the
    /// permitted account read model. No passwords or authentication secrets are exposed.
    /// </summary>
    public async Task<Result<PagedResponse<UserResponse>>> SearchAsync(
        UserQuery query, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(queryValidator, query, cancellationToken);
        if (error != null)
        {
            return Result<PagedResponse<UserResponse>>.Failure(error);
        }

        return await unitOfWork.ReadAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            return access.Error != null
                ? Result<PagedResponse<UserResponse>>.Failure(access.Error)
                : Result<PagedResponse<UserResponse>>.Success(await session.Accounts.SearchAsync(
                    query, timeProvider.GetUtcNow(), cancellationToken));
        }, cancellationToken);
    }

    /// <summary>
    /// Loads one permitted account by identity; reports not found without fabricating an empty
    /// account. Lock status is evaluated at the server time.
    /// </summary>
    public Task<Result<UserResponse>> GetAsync(long id, CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            if (access.Error != null)
            {
                return Result<UserResponse>.Failure(access.Error);
            }

            UserAccount? account = await session.Accounts.FindAsync(id, cancellationToken);
            return account == null
                ? Result<UserResponse>.Failure(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy tài khoản.", "NotFound"))
                : Result<UserResponse>.Success(UserMappings.ToResponse(
                    account, timeProvider.GetUtcNow()));
        }, cancellationToken);

    /// <summary>
    /// Checks confirmation, reason, target state and the self-action restriction, then saves the
    /// transition and before/after audit atomically. There is no SQL Version contract after DB-04
    /// was declined.
    /// </summary>
    public async Task<Result<UserResponse>> ChangeAsync(
        long id, AccountActionRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            actionValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<UserResponse>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<UserResponse>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền.", "Forbidden"));
            }

            UserAccount? target = await session.Accounts.FindAsync(id, cancellationToken);
            if (target == null)
            {
                return Result<UserResponse>.Failure(new Error(
                    "USER_NOT_FOUND", "Không tìm thấy tài khoản.", "NotFound"));
            }

            if (id == access.Value.Id)
            {
                return Result<UserResponse>.Failure(new Error(
                    "SELF_ACTION", "Không được thay đổi trạng thái hoặc mở khóa chính mình."));
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            bool eligible = request.Action switch
            {
                "suspend" => target.Status == "Active",
                "activate" => target.Status == "Suspended",
                "unlock" => target.IsTemporarilyLocked(now),
                _ => false
            };
            if (!eligible)
            {
                return Result<UserResponse>.Failure(new Error(
                    "ACCOUNT_CONFLICT", "Trạng thái đã thay đổi. Vui lòng tải lại.", "Conflict"));
            }

            string before = Snapshot(target);
            switch (request.Action)
            {
                case "suspend":
                    target.Suspend(request.Reason ?? string.Empty);
                    break;
                case "activate":
                    target.Activate(request.Reason ?? string.Empty);
                    break;
                case "unlock":
                    target.Unlock(now);
                    break;
            }

            await session.Accounts.SaveAsync(target, cancellationToken);
            await session.AddAuditAsync(new AuditRecord(
                access.Value.Id, "User", target.Id, request.Action, before, Snapshot(target),
                request.Reason?.Trim(), now), cancellationToken);
            return Result<UserResponse>.Success(UserMappings.ToResponse(target, now));
        }, cancellationToken);
    }

    /// <summary>
    /// Rechecks the actor account in the same unit of work. SystemAdmin is the explicit demo
    /// authority; CustomerSupport permissions remain undecided and are not inherited
    /// automatically.
    /// </summary>
    private Task<Result<UserAccount>> AuthorizeAsync(
        IThreeUcSession session, CancellationToken cancellationToken) =>
        CurrentUserAccess.RequireAsync(session, currentUser, "SystemAdmin",
            timeProvider.GetUtcNow(), cancellationToken);

    /// <summary>
    /// Serializes status, lock and mock session revocation state for the audit record; excludes
    /// password and token material.
    /// </summary>
    private static string Snapshot(UserAccount user) => JsonSerializer.Serialize(new
    {
        user.Status,
        user.LockedUntil,
        user.FailedLoginCount,
        user.SessionVersion
    });
}
