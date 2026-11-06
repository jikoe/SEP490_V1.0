using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Administration;

/// <summary>UC-ADM-03 Manage User Account Status and UC-ADM-05 Manage Product Categories.</summary>
public sealed class AdministrationServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    private readonly AdminStore _store = new();
    private readonly RecordingUnitOfWork _uow = new();

    public AdministrationServiceTests()
    {
        _store.Users[1] = new User { UserId = 1, Role = "SystemAdmin", Status = "Active" };
        _store.Users[2] = new User { UserId = 2, Role = "RetailCustomer", Status = "Active" };
        _store.Users[3] = new User { UserId = 3, Role = "VerificationOfficer", Status = "Active" };
        _store.Users[5] = new User { UserId = 5, Role = "RetailCustomer", Status = "Active" };
        _store.Categories[9] = new Category { CategoryId = 9, CategoryName = "Rau ăn lá", IsActive = true };
        _store.Settings[9] = new CategorySettings { CategoryId = 9 };
    }

    private AdministrationService Service(long actorId = 1, INotificationGateway? gateway = null) =>
        new(_store, _uow, new Actor(actorId), new FakeClock(T0), gateway,
            new AccountNotificationSettings("support@example.test"));

    // ---------- UC-ADM-03: AccountActionValidator / User rules ----------

    [Theory(DisplayName = "[UC-ADM-03 #06/#07] Reason length boundary 500/501")]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task AccountAction_ReasonLengthBoundary(int length, bool valid)
    {
        var result = await new AccountActionValidator().ValidateAsync(
            new AccountActionRequest("suspend", new string('a', length), true, "Active"));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName == "Reason"));
    }

    [Theory(DisplayName = "[UC-ADM-03 #16/#17] Temporary lock uses strict comparison at LockedUntil")]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public void IsTemporarilyLocked_ExactBoundary(int seconds, bool locked)
    {
        var user = new User { LockedUntil = T0.UtcDateTime };

        Assert.Equal(locked, user.IsTemporarilyLocked(T0.AddSeconds(seconds)));
    }

    // ---------- UC-ADM-03: SearchUsersAsync ----------

    [Fact(DisplayName = "[UC-ADM-03 #18] Non-administrator cannot search users")]
    public async Task SearchUsers_NotAdministrator_Forbidden()
    {
        var result = await Service(actorId: 5).SearchUsersAsync(new UserSearch(), default);

        Assert.Equal("FORBIDDEN", result.Error?.Code);
        Assert.Equal(0, _store.UserSearches);
    }

    [Theory(DisplayName = "[UC-ADM-03 #19/#20/#21] Page size boundary 0/100/101")]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task SearchUsers_PageSizeBoundary(int pageSize, bool accepted)
    {
        var result = await Service().SearchUsersAsync(new UserSearch(PageSize: pageSize), default);

        Assert.Equal(accepted, result.IsSuccess);
        Assert.Equal(accepted ? 1 : 0, _store.UserSearches);
        if (!accepted)
        {
            Assert.Equal("VALIDATION", result.Error!.Code);
        }
    }

    [Fact(DisplayName = "[UC-ADM-03 #22] Keyword longer than 100 characters is rejected")]
    public async Task SearchUsers_LongKeyword_Rejected()
    {
        var result = await Service().SearchUsersAsync(new UserSearch(Keyword: new string('a', 101)), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(0, _store.UserSearches);
    }

    // ---------- UC-ADM-03: ChangeAccountAsync ----------

    [Fact(DisplayName = "[UC-ADM-03 #23] Suspend active customer writes one audit and commits")]
    public async Task ChangeAccount_Suspend_AuditsAndCommits()
    {
        var result = await Service().ChangeAccountAsync(2, new("suspend", "Vi phạm chính sách", true, "Active"), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Suspended", _store.Users[2].Status);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal(("User", 2L, "suspend", "Vi phạm chính sách"), (audit.EntityType, audit.EntityId, audit.Action, audit.Reason));
        Assert.Contains("Active", audit.OldValue);
        Assert.Contains("Suspended", audit.NewValue);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-03 #24] Activate suspended customer")]
    public async Task ChangeAccount_Activate_Suspended()
    {
        _store.Users[2].Status = "Suspended";

        var result = await Service().ChangeAccountAsync(2, new("activate", "Đã xác minh", true, "Suspended"), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Active", _store.Users[2].Status);
        Assert.Equal("activate", Assert.Single(_store.Audits).Action);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-03 #25] Unlock clears lock and counter only")]
    public async Task ChangeAccount_Unlock_ClearsLock()
    {
        var user = _store.Users[2];
        user.LockedUntil = T0.UtcDateTime.AddHours(1);
        user.FailedLoginCount = 5;

        var result = await Service().ChangeAccountAsync(2, new("unlock", null, true, "Active", T0.AddHours(1)), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal("Active", user.Status);
        Assert.Equal("unlock", Assert.Single(_store.Audits).Action);
    }

    [Theory(DisplayName = "[UC-ADM-03 #26-#30] Self action, staff role, invalid state or missing user is refused")]
    [InlineData(1L, "suspend", "SELF_ACTION")]
    [InlineData(3L, "suspend", "ROLE_NOT_SUSPENDABLE")]
    [InlineData(2L, "activate", "ACTION_NOT_ALLOWED")]
    [InlineData(2L, "unlock", "ACTION_NOT_ALLOWED")]
    [InlineData(999L, "suspend", "NOT_FOUND")]
    public async Task ChangeAccount_Refused_WithoutAudit(long id, string action, string code)
    {
        var result = await Service().ChangeAccountAsync(id,
            new(action, action == "unlock" ? null : "Review", true, "Active"), default);

        Assert.Equal(code, result.Error?.Code);
        Assert.Empty(_store.Audits);
        Assert.Equal(0, _uow.Commits);
        Assert.Equal("Active", _store.Users[2].Status);
    }

    [Fact(DisplayName = "[UC-ADM-03 #31] Validation runs before the transaction")]
    public async Task ChangeAccount_BlankReason_NoTransaction()
    {
        var result = await Service().ChangeAccountAsync(2, new("suspend", "", true, "Active"), default);

        Assert.Contains("Reason", result.Error!.Fields!.Keys);
        Assert.Equal(0, _uow.Begins);
        Assert.Equal(0, _store.UserLookups);
    }

    [Fact(DisplayName = "[UC-ADM-03 #32] Missing displayed version is a MSG-UM-05 field error")]
    public async Task ChangeAccount_MissingExpectedStatus_IsRejected()
    {
        var result = await Service().ChangeAccountAsync(2, new("suspend", "Vi phạm", true), default);

        Assert.Contains("ExpectedStatus", result.Error!.Fields!.Keys);
        Assert.Equal(0, _uow.Begins);
    }

    [Fact(DisplayName = "[UC-ADM-03 #33] Stale displayed status is refused with MSG-UM-06 (BR-UM-12)")]
    public async Task ChangeAccount_StaleStatus_IsRefusedWithoutAudit()
    {
        var result = await Service().ChangeAccountAsync(2, new("activate", "Đã xác minh", true, "Suspended"), default);

        Assert.Equal("STATE_CONFLICT", result.Error?.Code);
        Assert.Contains("đã thay đổi", result.Error!.Message);
        Assert.Empty(_store.Audits);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-03 #34] A repeated confirmation is not applied twice")]
    public async Task ChangeAccount_RepeatedConfirmation_AppliesOnce()
    {
        AccountActionRequest request = new("suspend", "Vi phạm", true, "Active");

        Assert.True((await Service().ChangeAccountAsync(2, request, default)).IsSuccess);
        var second = await Service().ChangeAccountAsync(2, request, default);

        Assert.Equal("STATE_CONFLICT", second.Error?.Code);
        Assert.Single(_store.Audits);
        Assert.Single(_store.Notifications);
    }

    [Fact(DisplayName = "[UC-ADM-03 #35] Lock state is part of the displayed version")]
    public async Task ChangeAccount_LockChangedSinceDisplay_IsRefused()
    {
        _store.Users[2].LockedUntil = T0.UtcDateTime.AddMinutes(15);

        var result = await Service().ChangeAccountAsync(2, new("unlock", null, true, "Active", T0.AddMinutes(5)), default);

        Assert.Equal("STATE_CONFLICT", result.Error?.Code);
        Assert.NotNull(_store.Users[2].LockedUntil);
    }

    [Fact(DisplayName = "[UC-ADM-03 #36] Suspend uses the KYC-derived status: a Pending Farmer has no action")]
    public async Task ChangeAccount_PendingFarmer_HasNoSuspend()
    {
        _store.Users[6] = new User
        {
            UserId = 6, Role = "Farmer", Status = "Active",
            FarmerProfile = new FarmerProfile { VerificationStatus = "Pending" }
        };

        var result = await Service().ChangeAccountAsync(6, new("suspend", "Vi phạm", true, "Pending"), default);

        Assert.Equal("ACTION_NOT_ALLOWED", result.Error?.Code);
        Assert.Equal("Active", _store.Users[6].Status);
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "[UC-ADM-03 #37] Unlock works for an Active staff account (BR-UM-05)")]
    public async Task ChangeAccount_UnlockStaff_IsAllowed()
    {
        _store.Users[3].LockedUntil = T0.UtcDateTime.AddMinutes(10);

        var result = await Service().ChangeAccountAsync(3, new("unlock", null, true, "Active", T0.AddMinutes(10)), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(_store.Users[3].LockedUntil);
    }

    [Fact(DisplayName = "[UC-ADM-03 #38] Unlock is refused for a Suspended account")]
    public async Task ChangeAccount_UnlockSuspended_IsRefused()
    {
        _store.Users[2].Status = "Suspended";
        _store.Users[2].LockedUntil = T0.UtcDateTime.AddMinutes(10);

        var result = await Service().ChangeAccountAsync(2, new("unlock", null, true, "Suspended", T0.AddMinutes(10)), default);

        Assert.Equal("ACTION_NOT_ALLOWED", result.Error?.Code);
    }

    [Fact(DisplayName = "[UC-ADM-03 #39] Suspension email carries the reason and support contact (MSG-UM-08)")]
    public async Task ChangeAccount_EmailSent_ReturnsMsgUm08()
    {
        var gateway = new RecordingGateway();
        _store.Users[2].Email = "buyer@example.test";

        var result = await Service(gateway: gateway).ChangeAccountAsync(2,
            new("suspend", "Gian lận trong giao dịch", true, "Active"), default);

        Assert.True(result.Value!.EmailSent);
        Assert.Equal("Tài khoản #2 đã được tạm ngưng.", result.Value.Message);
        var (to, body) = Assert.Single(gateway.Sent);
        Assert.Equal("buyer@example.test", to);
        Assert.Contains("Gian lận trong giao dịch", body);
        Assert.Contains("support@example.test", body);
        var email = Assert.Single(_store.Notifications);
        Assert.Equal(("Email", "Sent"), (email.DeliveryChannel, email.DeliveryStatus));
    }

    [Fact(DisplayName = "[UC-ADM-03 #40] Email failure keeps the change and returns MSG-UM-09")]
    public async Task ChangeAccount_EmailFails_KeepsChange()
    {
        var gateway = new RecordingGateway { Fail = true };

        var result = await Service(gateway: gateway).ChangeAccountAsync(2,
            new("suspend", "Vi phạm", true, "Active"), default);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.EmailSent);
        Assert.Contains("gửi lại", result.Value.Message);
        Assert.Equal("Suspended", _store.Users[2].Status);
        var email = Assert.Single(_store.Notifications);
        Assert.Equal(("Failed", 1), (email.DeliveryStatus, email.RetryCount));
        Assert.Equal(AdministrationService.AccountEmailType, email.NotificationType);
    }

    [Fact(DisplayName = "[UC-ADM-03 #42] Suspending an account with open transactions tells Support and Finance (BR-UM-07)")]
    public async Task Suspend_OpenTransactions_FlagsSupportAndFinance()
    {
        _store.Users[7] = new User { UserId = 7, Role = "CustomerSupport", Status = "Active" };
        _store.Users[8] = new User { UserId = 8, Role = "FinanceStaff", Status = "Active" };
        _store.Users[9] = new User { UserId = 9, Role = "FinanceStaff", Status = "Suspended" };
        _store.Impact = new AccountImpactDto(2, 1, 350_000m);

        var result = await Service().ChangeAccountAsync(2, new("suspend", "Gian lận", true, "Active"), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var flags = _store.Notifications.Where(n => n.NotificationType == "AccountStatus").ToList();
        Assert.Equal(new long[] { 7, 8 }, flags.Select(n => n.UserId).Order());
        Assert.All(flags, n => Assert.Contains("2 đơn đang mở, 1 hợp đồng", n.Content));
        Assert.All(flags, n => Assert.Equal(2L, n.RelatedRecordId));
    }

    [Theory(DisplayName = "[UC-ADM-03 #43] No flag without open transactions or for Unlock")]
    [InlineData("suspend", 0)]
    [InlineData("unlock", 2)]
    public async Task NoOpenTransactionsOrUnlock_NoFlag(string action, int openOrders)
    {
        _store.Users[7] = new User { UserId = 7, Role = "CustomerSupport", Status = "Active" };
        if (action == "unlock") _store.Users[2].LockedUntil = T0.UtcDateTime.AddMinutes(10);
        _store.Impact = new AccountImpactDto(openOrders, 0, 0);

        var result = await Service().ChangeAccountAsync(2, action == "unlock"
            ? new("unlock", null, true, "Active", T0.AddMinutes(10))
            : new("suspend", "Vi phạm", true, "Active"), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.DoesNotContain(_store.Notifications, n => n.NotificationType == "AccountStatus");
    }

    [Fact(DisplayName = "[UC-ADM-03 #41] Action impact for MSG-UM-07 is admin-only")]
    public async Task GetAccountImpact_ReturnsCountsForAdministrator()
    {
        _store.Impact = new AccountImpactDto(2, 1, 350_000m);

        var denied = await Service(actorId: 5).GetAccountImpactAsync(2, default);
        var impact = await Service().GetAccountImpactAsync(2, default);

        Assert.Equal("FORBIDDEN", denied.Error?.Code);
        Assert.True(impact.Value!.HasOpenTransactions);
        Assert.Equal(350_000m, impact.Value.HeldPayoutAmount);
    }

    // ---------- UC-ADM-05: CategoryValidator / search ----------

    [Theory(DisplayName = "[UC-ADM-05 #04/#05] Category name length boundary 100/101")]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task CategoryName_LengthBoundary(int length, bool valid)
    {
        var result = await new CategoryValidator().ValidateAsync(
            new CategoryRequest(new string('a', length), "", true, false));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName == "Name"));
    }

    [Fact(DisplayName = "[UC-ADM-05 #06] Category search returns the repository page")]
    public async Task SearchCategories_Valid_ReturnsPage()
    {
        var result = await Service().SearchCategoriesAsync(new CategorySearch(Keyword: "rau"), default);

        Assert.True(result.IsSuccess);
        Assert.Same(_store.CategoryPage, result.Value);
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #07] Page 0 is rejected before querying")]
    public async Task SearchCategories_PageZero_Rejected()
    {
        var result = await Service().SearchCategoriesAsync(new CategorySearch(Page: 0), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(0, _store.CategorySearches);
    }

    [Fact(DisplayName = "[UC-ADM-05 #08] Non-administrator cannot search categories")]
    public async Task SearchCategories_NotAdministrator_Forbidden()
    {
        var result = await Service(actorId: 5).SearchCategoriesAsync(new CategorySearch(), default);

        Assert.Equal("FORBIDDEN", result.Error?.Code);
    }

    // ---------- UC-ADM-05: SaveCategoryAsync ----------

    [Fact(DisplayName = "[UC-ADM-05 #09] Create category saves, audits and commits")]
    public async Task SaveCategory_Create_Audited()
    {
        var result = await Service().SaveCategoryAsync(null, new("Rau củ", "Rau củ quả tươi", true, false), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var created = Assert.Single(_store.Added);
        Assert.Equal(("Rau củ", "Rau củ quả tươi", true), (created.CategoryName, created.Description, created.IsActive));
        Assert.Equal(2, _uow.Saves);
        Assert.Equal("Create", Assert.Single(_store.Audits).Action);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #10] Update category details is audited")]
    public async Task SaveCategory_Update_Audited()
    {
        var result = await Service().SaveCategoryAsync(9, new("Rau ăn lá", "Mô tả mới", true, false, Version9()), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Mô tả mới", _store.Categories[9].Description);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal("Update", audit.Action);
        Assert.DoesNotContain("Mô tả mới", audit.OldValue);
    }

    [Fact(DisplayName = "[UC-ADM-05 #11] Duplicate (trimmed) name is rejected")]
    public async Task SaveCategory_Duplicate_Rejected()
    {
        _store.ExistingNames.Add("Rau củ");

        var result = await Service().SaveCategoryAsync(null, new("  Rau củ  ", null, true, false), default);

        Assert.Equal("CATEGORY_DUPLICATE", result.Error?.Code);
        Assert.Empty(_store.Added);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #12] Status change without confirmation is rejected")]
    public async Task SaveCategory_UnconfirmedStatusChange_Rejected()
    {
        var result = await Service().SaveCategoryAsync(9, new("Rau ăn lá", null, false, false, Version9()), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.True(_store.Categories[9].IsActive);
    }

    [Fact(DisplayName = "[UC-ADM-05 #13] Archived category cannot be edited")]
    public async Task SaveCategory_Archived_Rejected()
    {
        _store.Settings[9].Archive();

        var result = await Service().SaveCategoryAsync(9, new("Tên mới", null, true, false, Version9()), default);

        Assert.Equal("ARCHIVED", result.Error?.Code);
        Assert.Equal("Rau ăn lá", _store.Categories[9].CategoryName);
        Assert.Equal(0, _uow.Saves);
    }

    [Fact(DisplayName = "[UC-ADM-05 #14] Missing category returns NOT_FOUND")]
    public async Task SaveCategory_Missing_NotFound()
    {
        var result = await Service().SaveCategoryAsync(404, new("Tên", null, true, false, "any"), default);

        Assert.Equal("NOT_FOUND", result.Error?.Code);
        Assert.Empty(_store.Audits);
    }

    // ---------- UC-ADM-05: ChangeCategoryAsync ----------

    [Theory(DisplayName = "[UC-ADM-05 #15/#16] Unconfirmed or unsupported action is rejected before the transaction")]
    [InlineData("deactivate", false)]
    [InlineData("rename", true)]
    [InlineData("archive", true)]
    public async Task ChangeCategory_InvalidRequest_Rejected(string action, bool confirmed)
    {
        var result = await Service().ChangeCategoryAsync(9, action, new ConfirmationRequest(confirmed, Version9()), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(0, _uow.Begins);
        Assert.True(_store.Categories[9].IsActive);
    }

    [Fact(DisplayName = "[UC-ADM-05 #17] Deactivate category")]
    public async Task ChangeCategory_Deactivate()
    {
        var result = await Service().ChangeCategoryAsync(9, "deactivate", new ConfirmationRequest(true, Version9()), default);

        Assert.True(result.IsSuccess);
        Assert.False(_store.Categories[9].IsActive);
        Assert.Equal("deactivate", Assert.Single(_store.Audits).Action);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #18] Delete unused category")]
    public async Task ChangeCategory_DeleteUnused()
    {
        var result = await Service().ChangeCategoryAsync(9, "delete", new ConfirmationRequest(true, Version9()), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(9, Assert.Single(_store.Deleted).CategoryId);
        Assert.Equal("delete", Assert.Single(_store.Audits).Action);
    }

    [Fact(DisplayName = "[UC-ADM-05 #19] Category with active products cannot be deleted")]
    public async Task ChangeCategory_ActiveProducts_InUse()
    {
        _store.ActiveProducts = 3;

        var result = await Service().ChangeCategoryAsync(9, "delete", new ConfirmationRequest(true, Version9()), default);

        Assert.Equal("CATEGORY_IN_USE", result.Error?.Code);
        Assert.Contains("còn 3 sản phẩm đang bán", result.Error!.Message);
        Assert.Empty(_store.Deleted);
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #20] Category with historical references is archived instead of deleted")]
    public async Task ChangeCategory_References_Archived()
    {
        _store.HasReferences = true;

        var result = await Service().ChangeCategoryAsync(9, "delete", new ConfirmationRequest(true, Version9()), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(_store.Deleted);
        Assert.False(_store.Categories[9].IsActive);
        Assert.True(_store.Settings[9].IsArchived);
        Assert.Equal("archive", Assert.Single(_store.Audits).Action);
        Assert.Equal("archived", result.Value!.Outcome);
    }

    [Fact(DisplayName = "[UC-ADM-05 #21] Editing without the displayed version is a field error")]
    public async Task SaveCategory_MissingVersion_Rejected()
    {
        var result = await Service().SaveCategoryAsync(9, new("Rau ăn lá", "Mới", true, false), default);

        Assert.Contains("Version", result.Error!.Fields!.Keys);
        Assert.Equal(0, _uow.Begins);
    }

    [Fact(DisplayName = "[UC-ADM-05 #22] A change made by another Administrator returns MSG122")]
    public async Task SaveCategory_StaleVersion_Conflict()
    {
        string shown = Version9();
        _store.Categories[9].Description = "Admin khác vừa sửa";

        var result = await Service().SaveCategoryAsync(9, new("Rau ăn lá", "Của tôi", true, false, shown), default);

        Assert.Equal("CATEGORY_CHANGED", result.Error?.Code);
        Assert.Equal("Admin khác vừa sửa", _store.Categories[9].Description);
        Assert.Empty(_store.Audits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #23] A repeated delete confirmation is refused after archiving")]
    public async Task ChangeCategory_RepeatedConfirmation_Conflict()
    {
        _store.HasReferences = true;
        var request = new ConfirmationRequest(true, Version9());

        Assert.True((await Service().ChangeCategoryAsync(9, "delete", request, default)).IsSuccess);
        var second = await Service().ChangeCategoryAsync(9, "delete", request, default);

        Assert.Equal("CATEGORY_CHANGED", second.Error?.Code);
        Assert.Single(_store.Audits);
    }

    [Fact(DisplayName = "[UC-ADM-05 #24] Duplicate name is reported on the name field (MSG-CAT-02)")]
    public async Task SaveCategory_Duplicate_FieldError()
    {
        _store.ExistingNames.Add("Rau củ");

        var result = await Service().SaveCategoryAsync(null, new("Rau củ", null, true, false), default);

        Assert.Equal("Đã có danh mục tên Rau củ.", Assert.Single(result.Error!.Fields!["Name"]));
    }

    [Fact(DisplayName = "[UC-ADM-05 #25] Unknown status filter is rejected")]
    public async Task SearchCategories_UnknownStatus_Rejected()
    {
        var result = await Service().SearchCategoriesAsync(new CategorySearch(Status: "Deleted"), default);

        Assert.Equal("VALIDATION", result.Error?.Code);
        Assert.Equal(0, _store.CategorySearches);
    }

    private string Version9() => Category.VersionOf(_store.Categories[9].CategoryName,
        _store.Categories[9].Description, _store.Categories[9].IsActive, _store.Settings[9].IsArchived);

    private sealed record Actor(long UserId) : IActorContext;

    private sealed class RecordingGateway : INotificationGateway
    {
        public bool Fail { get; init; }
        public List<(string To, string Body)> Sent { get; } = [];

        public Task SendEmailAsync(string recipient, string subject, string content, string eventKey,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("SMTP unavailable");
            }
            Sent.Add((recipient, content));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUseCaseUnitOfWork
    {
        public int Begins { get; private set; }
        public int Saves { get; private set; }
        public int Commits { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken)
        {
            Begins++;
            return Task.FromResult<ITransaction>(new Transaction(this));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }

        private sealed class Transaction(RecordingUnitOfWork owner) : ITransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken)
            {
                owner.Commits++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    internal sealed class AdminStore : IAdministrationRepository
    {
        public Dictionary<long, User> Users { get; } = [];
        public Dictionary<long, Category> Categories { get; } = [];
        public Dictionary<long, CategorySettings> Settings { get; } = [];
        public HashSet<string> ExistingNames { get; } = [];
        public List<Category> Added { get; } = [];
        public List<Category> Deleted { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public Page<CategoryDto> CategoryPage { get; } = new([], 0, 1, 10);
        public int ActiveProducts { get; set; }
        public bool HasReferences { get; set; }
        public int UserSearches { get; private set; }
        public int CategorySearches { get; private set; }
        public int UserLookups { get; private set; }

        public Task<User?> FindUserAsync(long id, CancellationToken ct)
        {
            UserLookups++;
            return Task.FromResult(Users.GetValueOrDefault(id));
        }

        public Task<User?> FindActorAsync(long id, CancellationToken ct) =>
            Task.FromResult(Users.GetValueOrDefault(id));

        public List<Notification> Notifications { get; } = [];
        public AccountImpactDto Impact { get; set; } = new(0, 0, 0);

        public Task<AccountImpactDto> GetAccountImpactAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Impact);

        public void AddNotification(Notification notification) => Notifications.Add(notification);

        public Task<IReadOnlyList<long>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roles,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<long>>(Users.Values
                .Where(user => user.Status == "Active" && roles.Contains(user.Role))
                .Select(user => user.UserId).ToList());

        public Task<Page<UserDto>> SearchUsersAsync(UserSearch search, long actorId, DateTimeOffset now,
            CancellationToken ct)
        {
            UserSearches++;
            return Task.FromResult(new Page<UserDto>([], 0, search.Page, search.PageSize, actorId));
        }

        public Task<UserDto?> GetUserAsync(long id, long actorId, DateTimeOffset now, CancellationToken ct) =>
            Task.FromResult(Users.GetValueOrDefault(id) is { } user
                ? new UserDto(user.UserId, user.FullName, null, user.Email, null, user.Role, user.Status, null,
                    now, user.IsTemporarilyLocked(now), null, user.FailedLoginCount, user.CanBeSuspended, actorId)
                : null);

        public Task<Category?> FindCategoryAsync(long id, CancellationToken ct) =>
            Task.FromResult(Categories.GetValueOrDefault(id));

        public Task<CategorySettings> GetCategorySettingsAsync(long id, CancellationToken ct) =>
            Task.FromResult(Settings.GetValueOrDefault(id) ?? new CategorySettings { CategoryId = id });

        public Task<Page<CategoryDto>> SearchCategoriesAsync(CategorySearch search, CancellationToken ct)
        {
            CategorySearches++;
            return Task.FromResult(CategoryPage);
        }

        public Task<CategoryDto?> GetCategoryAsync(long id, CancellationToken ct) =>
            Task.FromResult(Categories.GetValueOrDefault(id) is { } category
                ? new CategoryDto(category.CategoryId, category.CategoryName, category.Description,
                    category.IsActive, 0, Settings.GetValueOrDefault(id)?.IsArchived ?? false)
                : null);

        public Task<bool> CategoryNameExistsAsync(string name, long exceptId, CancellationToken ct) =>
            Task.FromResult(ExistingNames.Contains(name));

        public Task<int> CountActiveProductsAsync(long categoryId, CancellationToken ct) =>
            Task.FromResult(ActiveProducts);

        public Task<bool> HasCategoryReferencesAsync(long categoryId, CancellationToken ct) =>
            Task.FromResult(HasReferences);

        public void AddCategory(Category category, CategorySettings settings)
        {
            category.CategoryId = 100 + Added.Count;
            Added.Add(category);
            Categories[category.CategoryId] = category;
            Settings[category.CategoryId] = settings;
        }

        public void DeleteCategory(Category category, CategorySettings settings) => Deleted.Add(category);

        public void AddAudit(AuditLog audit) => Audits.Add(audit);
    }
}
