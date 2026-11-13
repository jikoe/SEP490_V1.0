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
    /// <summary>
    /// Queued account emails; the notification retry worker resends this type (JOB-09).
    /// </summary>
    public const string AccountEmailType = "AccountStatusEmail";

    /// <summary>
    /// Checks the caller in the database, not only the token: an Active, unlocked SystemAdmin / Administrator / Admin.
    /// Shared by UC-ADM-03 and UC-ADM-05.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when the caller may act.</returns>
    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    /// <summary>
    /// Step 1–2. User list for the Administrator: keyword (id, name, email, phone, farm or company name), role and
    /// account status (BR-UM-01), paged. Each row says whether it is the caller, locked, and may be suspended, so the
    /// screen only shows allowed actions.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Role, Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN for non-administrators; 400 VALIDATION for invalid paging or keyword.</returns>
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

    /// <summary>
    /// Step 3–4. One account with its displayed status, KYC status, lock state and failed login count.
    /// </summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The account; 403 / 404.</returns>
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

    /// <summary>
    /// Step 6: what a Suspend would affect, for MSG-UM-07 in the confirmation: open orders (as buyer or seller),
    /// active contracts and unsettled payout amount. Read-only.
    /// </summary>
    /// <param name="id">Account to inspect.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Counts and amount; 403 / 404.</returns>
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

    /// <summary>
    /// Step 7–10: Suspend, Reactivate or Unlock another account. Steps: 1) validate the request before any transaction
    /// (MSG-UM-05); 2) open a transaction and check the caller is an active Administrator (MSG-UM-13); 3) load the
    /// account (404); 4) refuse the caller's own account (MSG-UM-03); 5) the status and lock shown on the screen must
    /// still be current (BR-UM-12, MSG-UM-06); 6) the action must be allowed for the role and state (MSG-UM-04);
    /// 7) apply it — Suspend also revokes refresh tokens, Unlock clears the lock and failed count; 8) audit row, account
    /// email queued in the same transaction (BR-UM-10), Customer Support and Finance told about open transactions
    /// (BR-UM-07); 9) commit, then try to send the email once: MSG-UM-08 when sent, MSG-UM-09 when JOB-09 will retry.
    /// </summary>
    /// <param name="id">Account to change.</param>
    /// <param name="request">Action, Reason, Confirmed, ExpectedStatus and ExpectedLockedUntil.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The updated account, the message and whether the email was sent; or the error to show.</returns>
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
                case "suspend":
                    user.Suspend(reason!);
                    await repository.RevokeRefreshTokensForUserAsync(id, now, cancellationToken);
                    break;
                case "activate": user.Activate(reason!); break;
                case "unlock": user.Unlock(); break;
            }
            Audit("User", id, request.Action, before, AccountSnapshot(user), reason);
            // BR-UM-10: the email is enqueued in the same transaction as the change and its audit.
            email = AccountEmail(user, request.Action, reason, now.UtcDateTime);
            repository.AddNotification(email);
            await FlagOpenTransactionsAsync(user, request.Action, now.UtcDateTime, cancellationToken);
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

    private static readonly string[] s_supportAndFinanceRoles =
        ["CustomerSupport", "Customer Support", "FinanceStaff", "Finance Staff"];

    /// <summary>
    /// BR-UM-07 / BR-UM-08: open orders, contracts and unsettled payouts are not cancelled; Customer
    /// Support and Finance Staff are told which account changed so they review them. Payout holds
    /// are not stored: FarmerPayout.Blockers() refuses a Suspended Farmer when Finance pays.
    /// Does nothing for Unlock, for staff accounts, or when there is nothing open.
    /// </summary>
    /// <param name="user">Changed account.</param>
    /// <param name="action">suspend or activate.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    private async Task FlagOpenTransactionsAsync(User user, string action, DateTime now,
        CancellationToken cancellationToken)
    {
        if (action == "unlock" || !user.CanBeSuspended)
        {
            return;
        }
        var impact = await repository.GetAccountImpactAsync(user.UserId, cancellationToken);
        if (!impact.HasOpenTransactions)
        {
            return;
        }
        string who = $"{user.FullName} (#{user.UserId}, {user.Role})";
        string counts = $"{impact.OpenOrders} đơn đang mở, {impact.ActiveContracts} hợp đồng đang hiệu lực, "
            + $"{impact.HeldPayoutAmount:N0} ₫ payout chưa chi";
        var (title, content) = action == "suspend"
            ? ("Tài khoản bị tạm ngưng có giao dịch đang mở",
                $"Tài khoản {who} đã bị tạm ngưng. Hiện có {counts}. Các giao dịch này không bị hủy tự động; "
                    + "vui lòng xem lại. Payout của nông trại không đủ điều kiện chi cho tới khi tài khoản được kích hoạt lại.")
            : ("Tài khoản được kích hoạt lại có giao dịch đang mở",
                $"Tài khoản {who} đã được kích hoạt lại. Hiện có {counts}. Payout chỉ được chi khi không còn lý do "
                    + "giữ khác (khiếu nại chưa quyết định, tài khoản ngân hàng vừa thay đổi).");
        foreach (long staffId in await repository.GetActiveUserIdsByRolesAsync(s_supportAndFinanceRoles,
            cancellationToken))
        {
            repository.AddNotification(new Notification
            {
                UserId = staffId, Title = title, Content = content, NotificationType = "AccountStatus",
                IsRead = false, SentAt = now, DeliveryChannel = "InApp", DeliveryStatus = "Sent",
                RelatedRecordType = "User", RelatedRecordId = user.UserId
            });
        }
    }

    /// <summary>
    /// BR-UM-12: status and lock state must still be the ones shown at Step 4 (compared to the second).
    /// </summary>
    /// <param name="user">Account as stored.</param>
    /// <param name="request">Status and lock the screen showed.</param>
    /// <returns>True when the confirmation is still valid.</returns>
    private static bool MatchesDisplayedVersion(User user, AccountActionRequest request)
    {
        static long? Seconds(DateTime? value) =>
            value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
                .ToUnixTimeSeconds();
        return user.AccountStatus == request.ExpectedStatus
            && Seconds(user.LockedUntil) == request.ExpectedLockedUntil?.ToUnixTimeSeconds();
    }

    /// <summary>
    /// BR-UM-11: the account holder's email; a suspension includes the reason and support contact.
    /// Stored as "Sending" so the retry worker does not send it while this request is about to.
    /// </summary>
    /// <param name="user">Account holder.</param>
    /// <param name="action">suspend, activate or unlock.</param>
    /// <param name="reason">Reason, or an optional note for unlock.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The email notification, not yet staged.</returns>
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
    /// Failed for the notification retry worker (JOB-09). Without a gateway the email is left Pending for the worker.
    /// </summary>
    /// <param name="email">Email committed with the change.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when sent now.</returns>
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

    /// <summary>
    /// SD1. Category list: keyword on the name, status Active / Inactive / Archived, paged by name. Archived categories
    /// stay listed as read-only history (BR-CAT-01). Each row carries its version token for later actions.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN; 400 VALIDATION for invalid paging, keyword or status.</returns>
    public async Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<CategoryDto>>();
        }
        if (!ValidPage(search.Page, search.PageSize) || search.Keyword?.Length > 100
            || search.Status is not (null or "" or "Active" or "Inactive" or "Archived"))
        {
            return Invalid<Page<CategoryDto>>();
        }
        return UseCaseResult<Page<CategoryDto>>.Success(
            await repository.SearchCategoriesAsync(search, cancellationToken));
    }

    /// <summary>
    /// One category with product counts, archive flag and version token.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The category; 403 / 404.</returns>
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

    /// <summary>
    /// SD2. Create (id null) or edit a category. Steps: 1) CategoryValidator (name required ≤ 100, description ≤ 500);
    /// 2) editing needs the version the form was opened with; 3) transaction + Administrator check; 4) the category
    /// exists, is unchanged since it was shown (MSG122) and not archived; 5) changing Active/Inactive while editing
    /// needs Confirmed (MSG-CAT-03); 6) the trimmed name is unique (MSG-CAT-02, case-insensitive by collation);
    /// 7) apply, save once to get the new id, write the Create / Update audit row, save and commit.
    /// </summary>
    /// <param name="id">Category to edit, or null to create.</param>
    /// <param name="request">Name, Description, IsActive, Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The saved category (201 on create); or 400 / 403 / 404 / 409 CATEGORY_CHANGED / ARCHIVED.</returns>
    public async Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new CategoryValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<CategoryDto>.Validation(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        if (id.HasValue && string.IsNullOrWhiteSpace(request.Version))
        {
            return UseCaseResult<CategoryDto>.Validation(new Dictionary<string, string[]>
            {
                ["Version"] = ["Hãy tải lại danh mục trước khi lưu."]
            });
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
        if (id.HasValue && !MatchesVersion(category, settings, request.Version))
        {
            return CategoryChanged<CategoryDto>();
        }
        if (settings.IsArchived)
        {
            return Archived<CategoryDto>();
        }
        if (id.HasValue && category.IsActive != request.IsActive && !request.Confirmed)
        {
            return Invalid<CategoryDto>();
        }
        string name = request.Name.Trim();
        if (await repository.CategoryNameExistsAsync(name, id ?? 0, cancellationToken))
        {
            // MSG-CAT-02, shown under the name field (GBR-09).
            return new UseCaseResult<CategoryDto>(default, new Error("CATEGORY_DUPLICATE",
                $"Đã có danh mục tên {name}.", "Validation", new Dictionary<string, string[]>
                {
                    ["Name"] = [$"Đã có danh mục tên {name}."]
                }));
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

    /// <summary>
    /// SD3–SD4. Activate, deactivate or delete after confirmation. Steps: 1) Confirmed and a known action (400);
    /// 2) a version is required; 3) transaction + Administrator check; 4) the category exists (404), is unchanged
    /// (MSG122) and not archived (ARCHIVED); 5) delete: refused while published products remain (MSG-CAT-05 with
    /// their count); a category that was ever used (products, child categories, fee or commission policies) is
    /// archived instead — set Inactive, marked Archived, its category-specific fee policies ended (BR-CAT-04,
    /// BR-CAT-05); an unused one is deleted; 6) activate / deactivate only switch availability; 7) audit and commit.
    /// </summary>
    /// <param name="id">Category id.</param>
    /// <param name="action">activate, deactivate or delete.</param>
    /// <param name="request">Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Outcome activated / deactivated / archived / deleted with its message, or the error.</returns>
    public async Task<UseCaseResult<CategoryChangeResultDto>> ChangeCategoryAsync(long id, string action,
        ConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!request.Confirmed || action is not ("activate" or "deactivate" or "delete"))
        {
            return Invalid<CategoryChangeResultDto>();
        }
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return UseCaseResult<CategoryChangeResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["Version"] = ["Hãy tải lại danh mục trước khi xác nhận."]
            });
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<CategoryChangeResultDto>();
        }
        var category = await repository.FindCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return Missing<CategoryChangeResultDto>();
        }
        var settings = await repository.GetCategorySettingsAsync(id, cancellationToken);
        if (!MatchesVersion(category, settings, request.Version))
        {
            return CategoryChanged<CategoryChangeResultDto>();
        }
        if (settings.IsArchived)
        {
            return Archived<CategoryChangeResultDto>();
        }
        var before = CategorySnapshot(category, settings);
        string name = category.CategoryName;
        string outcome;
        string message;
        if (action == "delete")
        {
            int activeProducts = await repository.CountActiveProductsAsync(id, cancellationToken);
            if (activeProducts > 0)
            {
                // MSG-CAT-05
                return UseCaseResult<CategoryChangeResultDto>.Failure("CATEGORY_IN_USE",
                    $"Danh mục {name} còn {activeProducts} sản phẩm đang bán. Hãy chuyển danh mục "
                        + "hoặc ẩn các sản phẩm đó trước khi xóa.", "Conflict");
            }
            if (await repository.HasCategoryReferencesAsync(id, cancellationToken))
            {
                // BR-CAT-04: a used category is archived permanently instead of deleted.
                category.SetAvailability(false);
                settings.Archive();
                await repository.EndCategoryFeePoliciesAsync(id,
                    DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7)), cancellationToken);
                outcome = "archived";
                message = $"Danh mục {name} đã được sử dụng nên đã được lưu trữ; danh mục này chỉ còn xem được.";
            }
            else
            {
                repository.DeleteCategory(category, settings);
                outcome = "deleted";
                message = $"Đã xóa danh mục {name}.";
            }
        }
        else
        {
            category.SetAvailability(action == "activate");
            outcome = action == "activate" ? "activated" : "deactivated";
            message = action == "activate" ? $"Đã kích hoạt danh mục {name}." : $"Đã ngừng kích hoạt danh mục {name}.";
        }
        Audit("Category", id, outcome == "archived" ? "archive" : action, before,
            CategorySnapshot(category, settings), null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<CategoryChangeResultDto>.Success(new CategoryChangeResultDto(outcome, message));
    }

    /// <summary>
    /// Compares the version the screen sent with the one computed from the stored name, description, active and archived flags.
    /// </summary>
    /// <param name="category">Stored category.</param>
    /// <param name="settings">Its archive state.</param>
    /// <param name="version">Version from the screen.</param>
    /// <returns>True when nobody changed it in between.</returns>
    private static bool MatchesVersion(Category category, CategorySettings settings, string? version) =>
        Category.VersionOf(category.CategoryName, category.Description, category.IsActive, settings.IsArchived)
            == version;

    // MSG122: someone else changed or archived the category after it was shown.
    /// <summary>
    /// 409 CATEGORY_CHANGED (MSG122): someone else changed or archived the category after it was shown.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> CategoryChanged<T>() => UseCaseResult<T>.Failure("CATEGORY_CHANGED",
        "Danh mục này đã thay đổi hoặc thao tác đã được xử lý. Hãy tải lại để xem kết quả hiện tại; "
            + "chưa có thao tác nào được thực hiện thêm.", "Conflict");

    /// <summary>
    /// 409 ARCHIVED: an archived category is read-only and cannot be reactivated.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Archived<T>() => UseCaseResult<T>.Failure("ARCHIVED",
        "Danh mục đã lưu trữ chỉ xem được và không thể thay đổi hoặc kích hoạt lại.", "Conflict");

    /// <summary>
    /// Queues an audit row with the current Administrator and trace id. Shared by UC-ADM-03 and UC-ADM-05.
    /// </summary>
    /// <param name="type">User or Category.</param>
    /// <param name="id">Entity id.</param>
    /// <param name="action">Action name.</param>
    /// <param name="before">JSON before.</param>
    /// <param name="after">JSON after.</param>
    /// <param name="reason">Reason, when there is one.</param>
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

    /// <summary>
    /// JSON of the audited account fields: status, lock and failed login count.
    /// </summary>
    /// <param name="user">Account.</param>
    /// <returns>JSON text.</returns>
    private static string AccountSnapshot(User user) => JsonSerializer.Serialize(new
    {
        user.Status, user.LockedUntil, user.FailedLoginCount
    });

    /// <summary>
    /// JSON of the audited category fields: name, description, active and archived flags.
    /// </summary>
    /// <param name="category">Category.</param>
    /// <param name="settings">Its archive state.</param>
    /// <returns>JSON text.</returns>
    private static string CategorySnapshot(Category category, CategorySettings settings)
    {
        return JsonSerializer.Serialize(new
        {
            category.CategoryName, category.Description, category.IsActive, settings.IsArchived
        });
    }

    /// <summary>
    /// Page ≥ 1 and page size 1..100.
    /// </summary>
    /// <param name="page">Page number.</param>
    /// <param name="size">Page size.</param>
    /// <returns>True when valid.</returns>
    private static bool ValidPage(int page, int size) => page > 0 && size is > 0 and <= 100;

    /// <summary>
    /// 403 FORBIDDEN (MSG-UM-13).
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    /// <summary>
    /// 400 VALIDATION.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Invalid<T>() => UseCaseResult<T>.Failure(
        "VALIDATION", "Vui lòng kiểm tra dữ liệu và xác nhận thao tác.");

    /// <summary>
    /// 404 NOT_FOUND.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND", "Không tìm thấy bản ghi.", "NotFound");
}
