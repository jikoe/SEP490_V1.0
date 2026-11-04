using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Administration;

public sealed class AdministrationService(
    IAdministrationRepository repository,
    IUseCaseUnitOfWork unitOfWork,
    IActorContext actorContext,
    TimeProvider clock,
    INotificationGateway? notificationGateway = null,
    AccountNotificationSettings? notificationSettings = null) : IAdministrationService
{
    /// <summary>Queued account emails; the notification retry worker resends this type (JOB-09).</summary>
    public const string AccountEmailType = "AccountStatusEmail";

    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    public async Task<UseCaseResult<Page<UserDto>>> SearchUsersAsync(UserSearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<UserDto>>();
        }
        if (!ValidPage(search.Page, search.PageSize) || search.Keyword?.Length > 100)
        {
            return Invalid<Page<UserDto>>();
        }
        return UseCaseResult<Page<UserDto>>.Success(await repository.SearchUsersAsync(
            search, actorContext.UserId, clock.GetUtcNow(), cancellationToken));
    }

    public async Task<UseCaseResult<UserDto>> GetUserAsync(long id, CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<UserDto>();
        }
        var user = await repository.GetUserAsync(
            id, actorContext.UserId, clock.GetUtcNow(), cancellationToken);
        return user is null ? Missing<UserDto>() : UseCaseResult<UserDto>.Success(user);
    }

    /// <summary>Step 6: what a Suspend would affect, for MSG-UM-07 in the confirmation.</summary>
    public async Task<UseCaseResult<AccountImpactDto>> GetAccountImpactAsync(long id,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<AccountImpactDto>();
        }
        if (await repository.FindActorAsync(id, cancellationToken) is null)
        {
            return Missing<AccountImpactDto>();
        }
        return UseCaseResult<AccountImpactDto>.Success(
            await repository.GetAccountImpactAsync(id, cancellationToken));
    }

    public async Task<UseCaseResult<AccountActionResultDto>> ChangeAccountAsync(long id,
        AccountActionRequest request, CancellationToken cancellationToken)
    {
        var validation = await new AccountActionValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<AccountActionResultDto>.Validation(validation.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        var now = clock.GetUtcNow();
        Notification? email;
        await using (var transaction = await unitOfWork.BeginAsync(cancellationToken))
        {
            if (!await IsAdministratorAsync(cancellationToken))
            {
                return Denied<AccountActionResultDto>();
            }
            var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
            var user = await repository.FindUserAsync(id, cancellationToken);
            if (user is null)
            {
                return Missing<AccountActionResultDto>();
            }
            if (!user.CanBeManagedBy(actor!))
            {
                // MSG-UM-03
                return UseCaseResult<AccountActionResultDto>.Failure("SELF_ACTION",
                    "Bạn không thể tạm ngưng, kích hoạt lại hoặc mở khóa tài khoản của chính mình.",
                    "Forbidden");
            }
            if (!MatchesDisplayedVersion(user, request))
            {
                // BR-UM-12 / MSG-UM-06: changed by someone else, or this confirmation already applied.
                return UseCaseResult<AccountActionResultDto>.Failure("STATE_CONFLICT",
                    "Tài khoản này đã thay đổi hoặc thao tác đã được xử lý. Hãy tải lại trạng thái "
                        + "hiện tại trước khi xác nhận lại.", "Conflict");
            }
            if (!user.AllowsAccountAction(request.Action, now))
            {
                // MSG-UM-04
                return UseCaseResult<AccountActionResultDto>.Failure(
                    user.CanBeSuspended ? "ACTION_NOT_ALLOWED" : "ROLE_NOT_SUSPENDABLE",
                    "Thao tác này không áp dụng cho vai trò hoặc trạng thái hiện tại của tài khoản. "
                        + "Hãy tải lại và chọn thao tác phù hợp.", "Conflict");
            }
            string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            var before = AccountSnapshot(user);
            switch (request.Action)
            {
                case "suspend": user.Suspend(reason!); break;
                case "activate": user.Activate(reason!); break;
                case "unlock": user.Unlock(); break;
            }
            Audit("User", id, request.Action, before, AccountSnapshot(user), reason);
            // BR-UM-10: the email is enqueued in the same transaction as the change and its audit.
            email = AccountEmail(user, request.Action, reason, now.UtcDateTime);
            repository.AddNotification(email);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        bool sent = await TrySendAsync(email, cancellationToken);
        var account = await repository.GetUserAsync(id, actorContext.UserId, clock.GetUtcNow(),
            cancellationToken);
        string done = request.Action switch
        {
            "suspend" => "tạm ngưng", "activate" => "kích hoạt lại", _ => "mở khóa"
        };
        string message = sent
            ? $"Tài khoản #{id} đã được {done}." // MSG-UM-08
            : $"Tài khoản #{id} đã được cập nhật. Chưa gửi được email thông báo; hệ thống sẽ gửi lại "
                + "theo lịch gửi lại thông báo."; // MSG-UM-09
        return UseCaseResult<AccountActionResultDto>.Success(new AccountActionResultDto(account!, message, sent));
    }

    /// <summary>BR-UM-12: status and lock state must still be the ones shown at Step 4.</summary>
    private static bool MatchesDisplayedVersion(User user, AccountActionRequest request)
    {
        static long? Seconds(DateTime? value) =>
            value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
                .ToUnixTimeSeconds();
        return user.AccountStatus == request.ExpectedStatus
            && Seconds(user.LockedUntil) == request.ExpectedLockedUntil?.ToUnixTimeSeconds();
    }

    /// <summary>BR-UM-11: the account holder's email; a suspension includes the reason and support contact.</summary>
    private Notification AccountEmail(User user, string action, string? reason, DateTime now)
    {
        string support = string.IsNullOrWhiteSpace(notificationSettings?.SupportEmail)
            ? "bộ phận hỗ trợ AgriSustain" : notificationSettings.SupportEmail;
        var (title, body) = action switch
        {
            "suspend" => ("Tài khoản AgriSustain của bạn đã bị tạm ngưng",
                $"Tài khoản {user.Email} đã bị tạm ngưng và không thể đăng nhập hoặc sử dụng các chức năng "
                    + $"của nền tảng.\nLý do: {reason}\nNếu cần hỗ trợ, vui lòng liên hệ {support}."),
            "activate" => ("Tài khoản AgriSustain của bạn đã được kích hoạt lại",
                $"Tài khoản {user.Email} đã được kích hoạt lại.\nLý do: {reason}\n"
                    + "Bạn có thể đăng nhập và tiếp tục sử dụng nền tảng."),
            _ => ("Tài khoản AgriSustain của bạn đã được mở khóa",
                $"Khóa đăng nhập tạm thời của tài khoản {user.Email} đã được gỡ. Bạn có thể đăng nhập lại."
                    + (reason is null ? "" : $"\nGhi chú: {reason}"))
        };
        return new Notification
        {
            UserId = user.UserId,
            Title = title,
            Content = body,
            NotificationType = AccountEmailType,
            IsRead = false,
            SentAt = now,
            DeliveryChannel = "Email",
            // Claimed by this request, which sends it right after commit; the worker only takes
            // over a Sending row left for more than 5 minutes, so it is never sent twice.
            DeliveryStatus = "Sending",
            RelatedRecordType = "User",
            RelatedRecordId = user.UserId,
            PayloadJson = $"account:{user.UserId}:{action}:{now.Ticks}"
        };
    }

    /// <summary>
    /// Sends the queued email once right away. A failure keeps the change and leaves the email
    /// Failed for the notification retry worker (JOB-09).
    /// </summary>
    private async Task<bool> TrySendAsync(Notification email, CancellationToken cancellationToken)
    {
        if (notificationGateway is null)
        {
            // No sender in this host: hand the email to the worker instead of leaving it claimed.
            email.DeliveryStatus = "Pending";
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }
        bool sent;
        try
        {
            var recipient = await repository.FindActorAsync(email.UserId, cancellationToken);
            await notificationGateway.SendEmailAsync(recipient!.Email, email.Title, email.Content,
                email.PayloadJson!, cancellationToken);
            sent = true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            sent = false;
        }
        email.DeliveryStatus = sent ? "Sent" : "Failed";
        email.RetryCount = sent ? 0 : 1;
        email.SentAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return sent;
    }

    public async Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<CategoryDto>>();
        }
        if (!ValidPage(search.Page, search.PageSize) || search.Keyword?.Length > 100)
        {
            return Invalid<Page<CategoryDto>>();
        }
        return UseCaseResult<Page<CategoryDto>>.Success(
            await repository.SearchCategoriesAsync(search, cancellationToken));
    }

    public async Task<UseCaseResult<CategoryDto>> GetCategoryAsync(long id,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<CategoryDto>();
        }
        var category = await repository.GetCategoryAsync(id, cancellationToken);
        return category is null ? Missing<CategoryDto>() : UseCaseResult<CategoryDto>.Success(category);
    }

    public async Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new CategoryValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<CategoryDto>.Validation(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<CategoryDto>();
        }
        var category = id.HasValue
            ? await repository.FindCategoryAsync(id.Value, cancellationToken) : new Category();
        if (category is null)
        {
            return Missing<CategoryDto>();
        }
        var settings = id.HasValue
            ? await repository.GetCategorySettingsAsync(id.Value, cancellationToken)
            : new CategorySettings();
        if (settings.IsArchived)
        {
            return UseCaseResult<CategoryDto>.Failure("ARCHIVED", "Danh mục đã lưu trữ.", "Conflict");
        }
        if (id.HasValue && category.IsActive != request.IsActive && !request.Confirmed)
        {
            return Invalid<CategoryDto>();
        }
        if (await repository.CategoryNameExistsAsync(request.Name.Trim(), id ?? 0, cancellationToken))
        {
            return UseCaseResult<CategoryDto>.Failure("CATEGORY_DUPLICATE",
                "Tên danh mục đã tồn tại.", "Conflict");
        }
        var before = CategorySnapshot(category, settings);
        category.UpdateDetails(request.Name, request.Description);
        category.SetAvailability(request.IsActive);
        if (!id.HasValue)
        {
            repository.AddCategory(category, settings);
        }
        // Identity allocation and its audit are committed by the same explicit transaction.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Audit("Category", category.CategoryId, id.HasValue ? "Update" : "Create", before,
            CategorySnapshot(category, settings), null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCategoryAsync(category.CategoryId, cancellationToken);
    }

    public async Task<UseCaseResult<bool>> ChangeCategoryAsync(long id, string action, bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed || action is not ("activate" or "deactivate" or "delete" or "archive"))
        {
            return Invalid<bool>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<bool>();
        }
        var category = await repository.FindCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return Missing<bool>();
        }
        var settings = await repository.GetCategorySettingsAsync(id, cancellationToken);
        if (settings.IsArchived)
        {
            return UseCaseResult<bool>.Failure("ARCHIVED", "Danh mục đã lưu trữ.", "Conflict");
        }
        var before = CategorySnapshot(category, settings);
        if (action is "delete" or "archive")
        {
            if (await repository.HasActiveProductsAsync(id, cancellationToken))
            {
                return UseCaseResult<bool>.Failure("CATEGORY_IN_USE",
                    "Danh mục còn sản phẩm đang hoạt động; hãy chuyển sản phẩm trước.", "Conflict");
            }
            if (action == "archive"
                || await repository.HasCategoryReferencesAsync(id, cancellationToken))
            {
                category.SetAvailability(false);
                settings.Archive();
                action = "archive";
            }
            else
            {
                repository.DeleteCategory(category, settings);
            }
        }
        else
        {
            category.SetAvailability(action == "activate");
        }
        Audit("Category", id, action, before, CategorySnapshot(category, settings), null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<bool>.Success(true);
    }

    private void Audit(string type, long id, string action, string before, string after,
        string? reason)
    {
        repository.AddAudit(new AuditLog
        {
            EntityType = type, EntityId = id, Action = action,
            OldValue = before, NewValue = after, Reason = reason,
            PerformedByUserId = actorContext.UserId, OccurredAt = clock.GetUtcNow().UtcDateTime,
            TraceId = actorContext.TraceId
        });
    }

    private static string AccountSnapshot(User user) => JsonSerializer.Serialize(new
    {
        user.Status, user.LockedUntil, user.FailedLoginCount
    });

    private static string CategorySnapshot(Category category, CategorySettings settings)
    {
        return JsonSerializer.Serialize(new
        {
            category.CategoryName, category.Description, category.IsActive, settings.IsArchived
        });
    }

    private static bool ValidPage(int page, int size) => page > 0 && size is > 0 and <= 100;

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    private static UseCaseResult<T> Invalid<T>() => UseCaseResult<T>.Failure(
        "VALIDATION", "Vui lòng kiểm tra dữ liệu và xác nhận thao tác.");

    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND", "Không tìm thấy bản ghi.", "NotFound");
}
