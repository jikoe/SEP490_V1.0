using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Services.Checkout;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class SqlUseCaseTests : IAsyncLifetime
{
    private readonly string _databaseName = $"AgriSustain_UcTests_{Guid.NewGuid():N}";
    private readonly TestClock _clock = new();
    private AgriSustainContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = NewContext();
        await _context.Database.EnsureCreatedAsync();
        var admin = new User { UserId = 1, Username = "admin-test", Email = "admin@example.test",
            FullName = "Administrator Test", PasswordHash = "test-only", Role = "SystemAdmin",
            Status = "Active", CreatedAt = _clock.GetUtcNow().UtcDateTime };
        var buyer = new User { UserId = 2, Username = "retail-test", Email = "retail@example.test",
            FullName = "Retail Test", PasswordHash = "test-only", Role = "RetailCustomer",
            Status = "Active", CreatedAt = _clock.GetUtcNow().UtcDateTime };
        var farmerUser = new User { UserId = 3, Username = "farmer-test",
            Email = "farmer@example.test", FullName = "Farmer Test", PasswordHash = "test-only",
            Role = "Farmer", Status = "Active", CreatedAt = _clock.GetUtcNow().UtcDateTime };
        // SQL identities are generated in insertion order; no explicit identity insert is needed.
        admin.UserId = 0; buyer.UserId = 0; farmerUser.UserId = 0;
        _context.Users.AddRange(admin, buyer, farmerUser);
        await _context.SaveChangesAsync();
        var category = new Category { CategoryName = "Test produce", IsActive = true };
        var farm = new FarmerProfile { UserId = farmerUser.UserId, FarmName = "Test farm",
            Address = "Test address", VerificationStatus = "Approved", ProvinceCode = "TEST",
            RepresentativeName = "Farmer Test" };
        _context.AddRange(category, farm);
        await _context.SaveChangesAsync();
        var product = new Product { CategoryId = category.CategoryId, FarmerProfileId = farm.FarmerProfileId,
            ProductName = "Test vegetable", Unit = "kg", BaseRetailPrice = 10000,
            WholesaleMinQuantity = 10, WholesalePrice = 8000, ApprovalStatus = "Approved",
            IsPublished = true, CreatedAt = _clock.GetUtcNow().UtcDateTime };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        var season = new CropSeason { FarmerProfileId = farm.FarmerProfileId,
            ProductId = product.ProductId, SeasonName = "Test season", Status = "Harvesting",
            CropVariety = "Test variety", YieldUnit = "kg" };
        _context.CropSeasons.Add(season);
        await _context.SaveChangesAsync();
        _context.ProductImages.Add(new ProductImage { ProductId = product.ProductId,
            ImageUrl = "https://example.test/produce.png", IsPrimary = true });
        _context.HarvestBatches.Add(new HarvestBatch { ProductId = product.ProductId,
            CropSeasonId = season.CropSeasonId, BatchCode = "TEST-BATCH", Status = "Available",
            ActualHarvestDate = DateOnly.FromDateTime(_clock.GetUtcNow().DateTime.AddDays(-1)),
            StockEnteredAt = _clock.GetUtcNow().UtcDateTime, HarvestYield = 10, AvailableQuantity = 10,
            ExpirationDate = DateOnly.FromDateTime(_clock.GetUtcNow().DateTime.AddDays(10)) });
        var cart = new Cart { UserId = buyer.UserId, UpdatedAt = _clock.GetUtcNow().UtcDateTime };
        cart.CartItems.Add(new CartItem { ProductId = product.ProductId, Quantity = 2,
            IsSelected = true, AddedAt = _clock.GetUtcNow().UtcDateTime });
        _context.Carts.Add(cart);
        _context.RewardPointEntries.Add(new RewardPointEntry { UserId = buyer.UserId,
            EntryType = "Earn", Points = 1000, RequestKey = "test-earn",
            OccurredAt = _clock.GetUtcNow().UtcDateTime });
        _context.Vouchers.Add(new Voucher { VoucherCode = "TEST10", VoucherName = "Test 10%", DiscountType = "Percentage",
            DiscountValue = 10, MinOrderAmount = 1000, UsageLimit = 1, IsActive = true,
            StartDate = _clock.GetUtcNow().UtcDateTime.AddDays(-1),
            EndDate = _clock.GetUtcNow().UtcDateTime.AddDays(1) });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        if (!_context.Database.GetDbConnection().Database.StartsWith("AgriSustain_UcTests_",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a non-test database.");
        }
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ChangeAccount_SelfAction_DoesNotWriteAudit()
    {
        var result = await Admin().ChangeAccountAsync(1,
            new AccountActionRequest("suspend", "Test reason", true, "Active"), default);
        Assert.Equal("SELF_ACTION", result.Error?.Code);
        Assert.Empty(await _context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChangeAccount_Cancelled_DoesNotChangeAccount()
    {
        var result = await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("suspend", "Test reason", false, "Active"), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("Active", (await _context.Users.FindAsync(2L))!.Status);
        Assert.Empty(await _context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChangeAccount_SuspendAndActivate_WritesBeforeAndAfterAudit()
    {
        Assert.True((await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("suspend", "Test suspension", true, "Active"), default)).IsSuccess);
        Assert.True((await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("activate", "Test activation", true, "Suspended"), default)).IsSuccess);
        var logs = await _context.AuditLogs.OrderBy(x => x.AuditLogId).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains("Active", logs[0].OldValue);
        Assert.Contains("Suspended", logs[0].NewValue);
        Assert.Equal(1, logs[0].PerformedByUserId);
    }

    [Fact]
    public async Task ChangeAccount_Suspend_QueuesAccountEmailWithTheChange()
    {
        var result = await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("suspend", "Test reason", true, "Active"), default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value!.EmailSent);
        var email = await _context.Notifications.AsNoTracking()
            .SingleAsync(x => x.NotificationType == AdministrationService.AccountEmailType);
        Assert.Equal((2L, "Email", "Pending", 0), (email.UserId, email.DeliveryChannel, email.DeliveryStatus,
            email.RetryCount));
        Assert.Contains("Test reason", email.Content);
    }

    [Fact(DisplayName = "[UC-ADM-01] Dashboard queries run on SQL Server: orders, new users and pending work")]
    public async Task PlatformDashboard_TranslatesToSql()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var farm = await _context.FarmerProfiles.SingleAsync();
        var product = await _context.Products.SingleAsync();
        product.ApprovalStatus = "Pending";
        var order = new Order
        {
            BuyerUserId = 2, OrderCode = "ADM01-1", OrderType = "Retail", OrderDate = now.AddHours(-2),
            ShippingAddress = "Test address", OrderStatus = "Delivered", RecipientNameSnapshot = "Buyer",
            RecipientPhoneSnapshot = "0912345678", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
            TotalAmount = 100_000, FinalAmount = 100_000
        };
        var delivered = new SellerOrder { FarmerProfileId = farm.FarmerProfileId, SellerOrderCode = "SO-ADM01-1",
            Status = "Delivered", Subtotal = 60_000, FinalAmount = 60_000, DeliveredAt = now.AddHours(-1) };
        delivered.OrderStatusHistories.Add(new OrderStatusHistory { OldStatus = "Shipping", NewStatus = "Delivered",
            OccurredAt = now.AddHours(-1) });
        var cancelled = new SellerOrder { FarmerProfileId = farm.FarmerProfileId, SellerOrderCode = "SO-ADM01-2",
            Status = "Cancelled", Subtotal = 40_000, FinalAmount = 40_000 };
        order.SellerOrders.Add(delivered);
        // One seller order per farm per order (UQ_SellerOrder_Order_Farmer), so the cancelled one has its own order.
        var second = new Order
        {
            BuyerUserId = 2, OrderCode = "ADM01-2", OrderType = "Retail", OrderDate = now.AddHours(-2),
            ShippingAddress = "Test address", OrderStatus = "Cancelled", RecipientNameSnapshot = "Buyer",
            RecipientPhoneSnapshot = "0912345678", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
            TotalAmount = 40_000, FinalAmount = 40_000
        };
        second.SellerOrders.Add(cancelled);
        _context.Orders.AddRange(order, second);
        await _context.SaveChangesAsync();
        var repository = new PlatformDashboardRepository(_context);

        var stats = await repository.OrderStatsAsync(now.AddDays(-1), now.AddDays(1), default);
        var users = await repository.NewUsersAsync(now.AddDays(-1), now.AddDays(1), default);
        var pending = await repository.PendingWorkAsync(default);
        var item = await repository.PendingWorkItemAsync("Listing", product.ProductId, default);

        Assert.Equal(new OrderStatsDto(2, 1, 1, 100m, 1, 50m), stats);
        Assert.Equal(new NewUsersDto(1, 1, 0), users);
        var listing = Assert.Single(pending);
        Assert.Equal(("Listing", product.ProductId, "Farmer Test"), (listing.Type, listing.ReferenceId, listing.SubmittedBy));
        Assert.NotNull(item);
        Assert.Null(await repository.PendingWorkItemAsync("Complaint", 999, default));
    }

    [Fact(DisplayName = "[UC-ADM-09] Tier queries run on SQL Server and a never-purchased tier can be removed")]
    public async Task SubscriptionTiers_TranslateToSql()
    {
        var repository = new SubscriptionTierRepository(_context);
        var tier = new B2bsubscriptionPackage();
        tier.Apply("Gói Doanh nghiệp", "Mô tả", 90, 1_500_000, true, false, 50, true);
        repository.Add(tier);
        await _context.SaveChangesAsync();
        var now = _clock.GetUtcNow().UtcDateTime;

        var page = await repository.SearchAsync(new SubscriptionTierSearch("doanh", "Active"), now, default);
        bool duplicate = await repository.NameExistsAsync("GÓI DOANH NGHIỆP", 0, default);
        bool purchased = await repository.HasSubscriptionsAsync(tier.PackageId, default);
        int active = await repository.CountActiveSubscriptionsAsync(tier.PackageId, now, default);

        var row = Assert.Single(page.Items);
        Assert.Equal((tier.PackageId, 0, false, tier.Version()), (row.Id, row.ActiveSubscriptions, row.Purchased, row.Version));
        Assert.True(duplicate);
        Assert.False(purchased);
        Assert.Equal(0, active);
        repository.Remove(tier);
        await _context.SaveChangesAsync();
        Assert.Empty((await repository.SearchAsync(new SubscriptionTierSearch(), now, default)).Items);
    }

    [Fact(DisplayName = "[UC-PS-01 BR-PV-12] A failed creation audit keeps no voucher")]
    public async Task VoucherCreate_AuditFails_NothingKept()
    {
        var repository = new AgriSustain.Infrastructure.Repositories.VoucherRepository(_context);
        var voucher = new Voucher
        {
            VoucherCode = "ATOMIC1", VoucherName = "Atomic", DiscountType = Voucher.TypePercentage,
            DiscountValue = 10, MinOrderAmount = 0, UsageLimit = 10, PerCustomerLimit = 1, IsActive = true,
            StartDate = _clock.GetUtcNow().UtcDateTime.AddDays(1), EndDate = _clock.GetUtcNow().UtcDateTime.AddDays(10)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddWithAuditAsync(voucher,
            () => throw new InvalidOperationException("audit failed"), default));

        await using var check = NewContext();
        Assert.False(await check.Vouchers.AnyAsync(v => v.VoucherCode == "ATOMIC1"));
    }

    [Fact(DisplayName = "[UC-PS-01 BR-PV-12] Voucher and its creation audit are saved together")]
    public async Task VoucherCreate_SavesVoucherAndAudit()
    {
        var repository = new AgriSustain.Infrastructure.Repositories.VoucherRepository(_context);
        var voucher = new Voucher
        {
            VoucherCode = "ATOMIC2", VoucherName = "Atomic", DiscountType = Voucher.TypePercentage,
            DiscountValue = 10, MinOrderAmount = 0, UsageLimit = 10, PerCustomerLimit = 1, IsActive = true,
            StartDate = _clock.GetUtcNow().UtcDateTime.AddDays(1), EndDate = _clock.GetUtcNow().UtcDateTime.AddDays(10)
        };

        await repository.AddWithAuditAsync(voucher, () => repository.AddAudit(new AuditLog
        {
            EntityType = "Voucher", EntityId = voucher.VoucherId, Action = "CreateVoucher",
            OccurredAt = _clock.GetUtcNow().UtcDateTime
        }), default);

        await using var check = NewContext();
        long id = (await check.Vouchers.SingleAsync(v => v.VoucherCode == "ATOMIC2")).VoucherId;
        Assert.True(await check.AuditLogs.AnyAsync(a => a.EntityType == "Voucher" && a.EntityId == id
            && a.Action == "CreateVoucher"));
    }

    [Fact]
    public async Task GetAccountImpact_TranslatesToSql()
    {
        var result = await Admin().GetAccountImpactAsync(2, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0m, result.Value!.HeldPayoutAmount);
    }

    [Fact(DisplayName = "[UC-ADM-03] MSG-UM-07 counts in-progress and unpaid PendingPayment orders, not completed ones")]
    public async Task GetAccountImpact_CountsPendingPayment()
    {
        foreach (var (code, status) in new[] { ("IMP-1", "PendingPayment"), ("IMP-2", "Shipping"), ("IMP-3", "Completed") })
        {
            _context.Orders.Add(new Order
            {
                BuyerUserId = 2, OrderCode = code, OrderType = "Retail", OrderDate = _clock.GetUtcNow().UtcDateTime,
                ShippingAddress = "Test address", OrderStatus = status, RecipientNameSnapshot = "Buyer",
                RecipientPhoneSnapshot = "0912345678", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
                TotalAmount = 100_000, FinalAmount = 100_000
            });
        }
        await _context.SaveChangesAsync();

        var result = await Admin().GetAccountImpactAsync(2, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, result.Value!.OpenOrders);
    }

    [Fact]
    public async Task ChangeAccount_Unlock_ResetsOnlySecurityLock()
    {
        var user = (await _context.Users.FindAsync(2L))!;
        user.LockedUntil = _clock.GetUtcNow().UtcDateTime.AddMinutes(10);
        user.FailedLoginCount = 5;
        await _context.SaveChangesAsync();
        var result = await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("unlock", null, true, "Active", _clock.GetUtcNow().AddMinutes(10)), default);
        Assert.True(result.IsSuccess);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal("Active", user.Status);
    }

    [Fact]
    public async Task SearchUsers_ContactAndOrganization_ReturnsSafePaginatedDto()
    {
        var result = await Admin().SearchUsersAsync(new UserSearch("Test farm"), default);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal("Test farm", result.Value.Items[0].OrganizationName);
    }

    [Fact]
    public async Task ChangeAccount_SuspendStaff_IsRejectedWithoutAudit()
    {
        var staff = new User { Username = "officer-test", Email = "officer@example.test",
            FullName = "Officer Test", PasswordHash = "test-only", Role = "VerificationOfficer",
            Status = "Active", CreatedAt = _clock.GetUtcNow().UtcDateTime };
        _context.Users.Add(staff);
        await _context.SaveChangesAsync();
        var result = await Admin().ChangeAccountAsync(staff.UserId,
            new AccountActionRequest("suspend", "Test reason", true, "Active"), default);
        Assert.Equal("ROLE_NOT_SUSPENDABLE", result.Error?.Code);
        Assert.Equal("Active", (await _context.Users.AsNoTracking()
            .SingleAsync(x => x.UserId == staff.UserId)).Status);
        Assert.Empty(await _context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SearchUsers_AccountStatus_DerivesFromKyc()
    {
        var farm = await _context.FarmerProfiles.SingleAsync();
        farm.VerificationStatus = "Pending";
        await _context.SaveChangesAsync();
        var pending = await Admin().SearchUsersAsync(new UserSearch(Status: "Pending"), default);
        Assert.Equal(3, Assert.Single(pending.Value!.Items).Id);
        var active = await Admin().SearchUsersAsync(new UserSearch(Status: "Active"), default);
        Assert.Equal(new long[] { 1, 2 }, active.Value!.Items.Select(x => x.Id));
        Assert.All(active.Value.Items, x => Assert.Null(x.VerificationStatus));
        Assert.False(active.Value.Items[0].CanSuspend);
        Assert.True(active.Value.Items[1].CanSuspend);
    }

    [Theory]
    [InlineData("Test produce")]
    [InlineData("  TEST PRODUCE ")]
    public async Task SaveCategory_DuplicateName_IsRejectedCaseInsensitively(string name)
    {
        var result = await Admin().SaveCategoryAsync(null,
            new CategoryRequest(name, null, true, false), default);
        Assert.Equal("CATEGORY_DUPLICATE", result.Error?.Code);
        Assert.Equal(1, await _context.Categories.CountAsync());
    }

    [Fact]
    public async Task SaveCategory_LegacyChild_IsFlattenedOnUpdate()
    {
        var child = new Category { CategoryName = "Test child", ParentCategoryId = 1, IsActive = true };
        _context.Categories.Add(child);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var update = await Admin().SaveCategoryAsync(child.CategoryId,
            new CategoryRequest("Test child", "Updated", true, false, await CategoryVersion(child.CategoryId)), default);
        Assert.True(update.IsSuccess);
        var saved = await _context.Categories.AsNoTracking()
            .SingleAsync(x => x.CategoryId == child.CategoryId);
        Assert.Null(saved.ParentCategoryId);
        Assert.Equal("Updated", saved.Description);
    }

    [Fact]
    public async Task DeleteCategory_Unused_RemovesCategoryAndWritesAudit()
    {
        var created = await Admin().SaveCategoryAsync(null,
            new CategoryRequest("Test unused", null, true, false), default);
        Assert.True(created.IsSuccess, created.Error?.Message);
        var result = await Admin().ChangeCategoryAsync(created.Value!.Id, "delete",
            new ConfirmationRequest(true, created.Value.Version), default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await _context.Categories.AnyAsync(x => x.CategoryId == created.Value.Id));
        Assert.Contains(await _context.AuditLogs.ToListAsync(), x => x.Action == "delete");
    }

    [Fact]
    public async Task DeleteCategory_ActiveProduct_IsRejected()
    {
        var result = await Admin().ChangeCategoryAsync(1, "delete",
            new ConfirmationRequest(true, await CategoryVersion(1)), default);
        Assert.Equal("CATEGORY_IN_USE", result.Error?.Code);
        Assert.Contains("còn 1 sản phẩm đang bán", result.Error!.Message);
        Assert.NotNull(await _context.Categories.FindAsync(1L));
    }

    [Fact]
    public async Task DeleteCategory_HistoricalProduct_ArchivesAndRetainsReference()
    {
        (await _context.Products.SingleAsync()).IsPublished = false;
        await _context.SaveChangesAsync();
        var archived = await Admin().ChangeCategoryAsync(1, "delete",
            new ConfirmationRequest(true, await CategoryVersion(1)), default);
        Assert.Equal("archived", archived.Value?.Outcome);
        Assert.True((await _context.CategorySettings.SingleAsync()).IsArchived);
        Assert.Equal(1, (await _context.Products.SingleAsync()).CategoryId);

        // BR-CAT-01: the archived category stays listed as read-only history.
        var listed = await Admin().SearchCategoriesAsync(new CategorySearch(Status: "Archived"), default);
        var row = Assert.Single(listed.Value!.Items);
        Assert.True(row.IsArchived);
        Assert.Empty((await Admin().SearchCategoriesAsync(new CategorySearch(Status: "Active"), default))
            .Value!.Items);
    }

    private async Task<string> CategoryVersion(long id) =>
        (await Admin().GetCategoryAsync(id, default)).Value!.Version;

    [Fact]
    public async Task ReviewedProducts_AreOrderedByDecisionTimeAndIncludeReviewer()
    {
        var existingProduct = await _context.Products.SingleAsync();
        existingProduct.ApprovalStatus = "Approved";
        existingProduct.IsPublished = true;

        var olderProduct = new Product
        {
            CategoryId = existingProduct.CategoryId,
            FarmerProfileId = existingProduct.FarmerProfileId,
            ProductName = "Older product reviewed most recently",
            Unit = "kg",
            BaseRetailPrice = 10000,
            ApprovalStatus = "Rejected",
            IsPublished = false,
            CreatedAt = _clock.GetUtcNow().UtcDateTime.AddDays(-10)
        };
        _context.Products.Add(olderProduct);
        await _context.SaveChangesAsync();

        var decisionTime = _clock.GetUtcNow().UtcDateTime;
        _context.AuditLogs.AddRange(
            new AuditLog
            {
                PerformedByUserId = 1,
                EntityType = "Product",
                EntityId = existingProduct.ProductId,
                Action = "ApproveProduct",
                OldValue = "Pending",
                NewValue = "Approved",
                OccurredAt = decisionTime.AddMinutes(-1)
            },
            new AuditLog
            {
                PerformedByUserId = 1,
                EntityType = "Product",
                EntityId = olderProduct.ProductId,
                Action = "RejectProduct",
                OldValue = "Pending",
                NewValue = "Rejected",
                Reason = "Test rejection",
                OccurredAt = decisionTime
            });
        await _context.SaveChangesAsync();

        var result = await new ProductModerationRepository(_context)
            .GetListAsync("Reviewed", null, 1, 10, default);

        Assert.Equal(olderProduct.ProductId, result.Items[0].Id);
        Assert.Equal(decisionTime, result.Items[0].ReviewedAt);
        Assert.Equal("Administrator Test", result.Items[0].ReviewedBy);
    }

    [Fact]
    public async Task Quote_InvalidVoucher_ClearsDiscountWithoutReservingStock()
    {
        var result = await Checkout().QuoteAsync(Input() with { VoucherCode = "INVALID" }, default);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Input.VoucherCode);
        Assert.Contains("MSG116", result.Value.Warning);
        Assert.Equal(10, (await _context.HarvestBatches.SingleAsync()).AvailableQuantity);
        Assert.Empty(await _context.Orders.ToListAsync());
    }

    [Fact]
    public async Task Quote_UnsupportedAddress_PreservesCart()
    {
        var input = Input();
        // A catalog ward outside the configured delivery areas;
        // unknown codes fail earlier with ADDRESS_INVALID.
        var result = await Checkout().QuoteAsync(input with
        {
            Address = input.Address with { WardCode = "20", Ward = "Phường Trương Định" }
        }, default);
        Assert.Equal("MSG117", result.Error?.Code);
        Assert.Single(await _context.CartItems.ToListAsync());
    }

    [Fact]
    public async Task Place_ValidQuote_CommitsStockCartVoucherAndPointHoldAtomically()
    {
        var service = Checkout();
        var quote = await service.QuoteAsync(Input() with { VoucherCode = "TEST10", Points = 100 }, default);
        Assert.True(quote.IsSuccess, quote.Error?.Message);
        var result = await service.PlaceAsync(Placement(quote.Value!), default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        _context.ChangeTracker.Clear();
        var order = await _context.Orders.Include(x => x.SellerOrders).SingleAsync();
        Assert.Equal("PendingPayment", order.OrderStatus);
        Assert.Equal(27900, order.FinalAmount);
        Assert.Equal(order.FinalAmount, order.SellerOrders.Sum(x => x.FinalAmount));
        Assert.Empty(await _context.CartItems.ToListAsync());
        var batch = await _context.HarvestBatches.SingleAsync();
        Assert.Equal(8, batch.AvailableQuantity);
        Assert.Equal(2, batch.ReservedQuantity);
        Assert.Equal(900, await new CheckoutRepository(_context).PointsAsync(2, default));
        Assert.Equal("Reserved", (await _context.VoucherRedemptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Place_RepeatedRequestAcrossContexts_ReturnsSameOrderWithoutSecondReservation()
    {
        var quote = await Checkout().QuoteAsync(Input(), default);
        var request = Placement(quote.Value!);
        var first = await Checkout().PlaceAsync(request, default);
        await using var other = NewContext();
        var second = await Checkout(other).PlaceAsync(request, default);
        Assert.True(second.IsSuccess);
        Assert.True(second.Value!.IsDuplicate);
        Assert.Equal(first.Value!.Id, second.Value.Id);
        Assert.Single(await other.Orders.ToListAsync());
        Assert.Equal(2, (await other.HarvestBatches.SingleAsync()).ReservedQuantity);
    }

    [Fact]
    public async Task Place_PriceChanged_RejectsWithoutPartialWrite()
    {
        var quote = await Checkout().QuoteAsync(Input(), default);
        var product = await _context.Products.SingleAsync();
        product.BaseRetailPrice += 1000;
        await _context.SaveChangesAsync();
        var result = await Checkout().PlaceAsync(Placement(quote.Value!), default);
        Assert.Equal("MSG118", result.Error?.Code);
        Assert.Empty(await _context.Orders.ToListAsync());
        Assert.Single(await _context.CartItems.ToListAsync());
        Assert.Equal(10, (await _context.HarvestBatches.SingleAsync()).AvailableQuantity);
    }

    [Fact]
    public async Task Place_AnotherBuyerQuote_IsNotFound()
    {
        var quote = await Checkout().QuoteAsync(Input(), default);
        var other = new User { Username = "other", Email = "other@example.test", FullName = "Other",
            Role = "RetailCustomer", Status = "Active", PasswordHash = "test" };
        _context.Users.Add(other);
        await _context.SaveChangesAsync();
        var result = await Checkout(userId: other.UserId).PlaceAsync(Placement(quote.Value!), default);
        Assert.Equal("NOT_FOUND", result.Error?.Code);
    }

    [Fact]
    public async Task Expire_PendingOrder_ReleasesStockVoucherAndPointsExactlyOnce()
    {
        var quote = await Checkout().QuoteAsync(Input() with { VoucherCode = "TEST10", Points = 100 }, default);
        Assert.True((await Checkout().PlaceAsync(Placement(quote.Value!), default)).IsSuccess);
        _clock.Advance(TimeSpan.FromMinutes(16));
        var expiry = new ReservationExpiryService(new CheckoutRepository(_context),
            new AdministrationRepository(_context), new SqlUnitOfWork(_context), _clock);
        Assert.Equal(1, await expiry.ExpireAsync(default));
        Assert.Equal(0, await expiry.ExpireAsync(default));
        _context.ChangeTracker.Clear();
        Assert.Equal("Cancelled", (await _context.Orders.SingleAsync()).OrderStatus);
        Assert.Equal(10, (await _context.HarvestBatches.SingleAsync()).AvailableQuantity);
        Assert.Equal(1000, await new CheckoutRepository(_context).PointsAsync(2, default));
        Assert.Equal("Released", (await _context.VoucherRedemptions.SingleAsync()).Status);
    }

    [Fact(DisplayName = "[UC-FIN-05 BR-FEE-05/08] Placing an order records seller fee lines; expiry reverses them")]
    public async Task PlatformFees_RecordedAtCheckout_ReversedOnExpiry()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        long categoryId = (await _context.Categories.SingleAsync()).CategoryId;
        var commission = new FeeType { FeeCode = "PLATFORM_COMMISSION", FeeName = "Hoa hồng", FeeGroup = FeeType.GroupPlatformCommission,
            RefundMode = FeeType.RefundProportional, Status = FeeType.StatusActive, CreatedAt = now };
        commission.Policies.Add(new FeePolicy { PolicyCode = "COM-ALL", Method = FeePolicy.MethodPercentage,
            CalculationBase = FeePolicy.BaseItemSubtotal, Rate = 5, OrderType = "Retail", EffectiveFrom = today.AddDays(-10),
            ApprovalStatus = FeePolicy.ApprovalApproved, Reason = "test", CreatedByUserId = 1, CreatedAt = now, RequestKey = "fee-1" });
        var specific = new FeePolicy { PolicyCode = "COM-CAT", Method = FeePolicy.MethodPercentage,
            CalculationBase = FeePolicy.BaseItemSubtotal, Rate = 10, OrderType = "Retail", AppliesToAllCategories = false,
            EffectiveFrom = today.AddDays(-1), ApprovalStatus = FeePolicy.ApprovalApproved, Reason = "test",
            CreatedByUserId = 1, CreatedAt = now, RequestKey = "fee-2" };
        specific.Categories.Add(new FeePolicyCategory { CategoryId = categoryId });
        commission.Policies.Add(specific);
        var tax = new FeeType { FeeCode = "TAX_VAT", FeeName = "Thuế GTGT", FeeGroup = FeeType.GroupTaxWithholding,
            RefundMode = FeeType.RefundProportional, IsMandatory = true, Status = FeeType.StatusActive, CreatedAt = now };
        tax.Policies.Add(new FeePolicy { PolicyCode = "VAT-ALL", Method = FeePolicy.MethodPercentage,
            CalculationBase = FeePolicy.BaseItemSubtotal, Rate = 1, OrderType = "Retail", EffectiveFrom = today,
            ApprovalStatus = FeePolicy.ApprovalApproved, Reason = "test", CreatedByUserId = 1, CreatedAt = now, RequestKey = "fee-3" });
        _context.FeeTypes.AddRange(commission, tax);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var quote = await Checkout(withFees: true).QuoteAsync(Input(), default);
        Assert.True((await Checkout(withFees: true).PlaceAsync(Placement(quote.Value!), default)).IsSuccess);
        _context.ChangeTracker.Clear();

        // 2 kg × 10 000 ₫: the category rate (10%) overrides the all-categories rate; VAT 1%.
        var fees = await _context.OrderItemFees.OrderBy(fee => fee.FeeCode).ToListAsync();
        Assert.Equal(new[] { ("PLATFORM_COMMISSION", 2000m), ("TAX_VAT", 200m) }, fees.Select(fee => (fee.FeeCode, fee.Amount)));
        // BR-FEE-09: the buyer pays only goods and delivery (2 × 10 000 + 10 000 ₫); fees come out of the Farmer's share.
        Assert.Equal(30_000m, (await _context.Orders.SingleAsync()).FinalAmount);

        _clock.Advance(TimeSpan.FromMinutes(16));
        var expiry = new ReservationExpiryService(new CheckoutRepository(_context), new AdministrationRepository(_context),
            new SqlUnitOfWork(_context), _clock, new OrderFeeService(new FeeRepository(_context)));
        Assert.Equal(1, await expiry.ExpireAsync(default));
        _context.ChangeTracker.Clear();
        Assert.All(await _context.OrderItemFees.ToListAsync(), fee => Assert.Equal(fee.Amount, fee.ReversedAmount));
    }

    [Fact]
    public async Task Place_InsufficientStock_RejectsWithoutCreatingOrder()
    {
        var quote = await Checkout().QuoteAsync(Input(), default);
        (await _context.HarvestBatches.SingleAsync()).AvailableQuantity = 1;
        await _context.SaveChangesAsync();
        var result = await Checkout().PlaceAsync(Placement(quote.Value!), default);
        Assert.Equal("MSG88", result.Error?.Code);
        Assert.Empty(await _context.Orders.ToListAsync());
    }

    private AgriSustainContext NewContext()
    {
        var server = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER")
            ?? "(localdb)\\MSSQLLocalDB";
        return new AgriSustainContext(new DbContextOptionsBuilder<AgriSustainContext>()
            .UseSqlServer($"Server={server};Database={_databaseName};Integrated Security=True;"
                + "TrustServerCertificate=True;Connect Timeout=30").Options);
    }

    private AdministrationService Admin() => new(new AdministrationRepository(_context),
        new SqlUnitOfWork(_context), new TestActor(1), _clock);

    private CheckoutService Checkout(AgriSustainContext? context = null, long userId = 2, bool withFees = false)
    {
        var database = context ?? _context;
        var policy = new CheckoutPolicy
        {
            PointValueVnd = 1, MaximumPointDiscountPercent = 50,
            Areas = [new DeliveryArea("100000", "Hà Nội", "Quận Hai Bà Trưng", "19",
                "Phường Quỳnh Lôi", 10000, 20000, 2, 1)]
        };
        return new CheckoutService(new CheckoutRepository(database),
            new AdministrationRepository(database), new SqlUnitOfWork(database), new TestActor(userId),
            _clock, policy, new TestAddressCatalog(), new PolicyShippingRateProvider(policy),
            withFees ? new OrderFeeService(new FeeRepository(database)) : null);
    }

    private static CheckoutRequest Input() => new(new CheckoutAddress("Test Customer",
        "0912345678", "100000", "Hà Nội", "100300", "Quận Hai Bà Trưng", "19",
        "Phường Quỳnh Lôi", "Test delivery address"), "Standard", "SePay", null, 0, null);

    private static PlaceOrderRequest Placement(CheckoutQuoteDto quote)
    {
        return new PlaceOrderRequest(quote.Id, Guid.NewGuid().ToString("D"), true);
    }

    private sealed record TestActor(long UserId) : IActorContext;

    /// <summary>Offline stand-in for the Goship city/district/ward catalog.</summary>
    private sealed class TestAddressCatalog : IAddressCatalog
    {
        private static readonly ProvinceOption[] Provinces = [new("100000", "Hà Nội")];

        private static readonly DistrictOption[] Districts =
        [
            new("100300", "100000", "Quận Hai Bà Trưng"), new("101500", "100000", "Quận Hà Đông")
        ];

        private static readonly WardOption[] Wards =
        [
            new("19", "100300", "Phường Quỳnh Lôi"), new("20", "100300", "Phường Trương Định")
        ];

        public Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProvinceOption>>(Provinces);

        public Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DistrictOption>>(
            Districts.Where(x => x.ProvinceCode == provinceCode).ToList());

        public Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<WardOption>>(
            Wards.Where(x => x.DistrictCode == districtCode).ToList());

        public Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address,
            CancellationToken cancellationToken)
        {
            var province = Provinces.SingleOrDefault(x => x.Code == address.ProvinceCode);
            var district = Districts.SingleOrDefault(x => x.Code == address.DistrictCode
                && x.ProvinceCode == address.ProvinceCode);
            var ward = Wards.SingleOrDefault(x => x.Code == address.WardCode
                && x.DistrictCode == address.DistrictCode);
            return Task.FromResult(province is null || district is null || ward is null ? null
                : address with
                {
                    Province = province.Name, District = district.Name, Ward = ward.Name,
                    CatalogVersion = "test"
                });
        }
    }

    private sealed class PolicyShippingRateProvider(CheckoutPolicy policy)
        : IShippingRateProvider
    {
        public int EstimatedDeliveryDays(string deliveryMethod) => deliveryMethod == "Standard"
            ? policy.Areas[0].StandardDays : policy.Areas[0].ExpressDays;

        public Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request,
            CancellationToken cancellationToken)
        {
            var area = policy.Areas.SingleOrDefault(x => x.ProvinceCode == request.Recipient.ProvinceCode
                && x.WardCode == request.Recipient.WardCode);
            decimal? fee = area is null ? null : request.DeliveryMethod == "Standard"
                ? area.StandardFee : area.ExpressFee;
            // Like Goship with no matching service, an address outside the served areas is "not supported" (MSG117);
            // null is reserved for an unavailable carrier.
            return Task.FromResult<ShippingRateResult?>(
                new ShippingRateResult(area is not null, fee));
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 22, 6, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
