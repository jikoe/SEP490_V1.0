using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Services.Checkout;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Data.Geography;
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
        _context.Vouchers.Add(new Voucher { VoucherCode = "TEST10", DiscountType = "Percentage",
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
            new AccountActionRequest("suspend", "Test reason", true), default);
        Assert.Equal("SELF_ACTION", result.Error?.Code);
        Assert.Empty(await _context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChangeAccount_Cancelled_DoesNotChangeAccount()
    {
        var result = await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("suspend", "Test reason", false), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("Active", (await _context.Users.FindAsync(2L))!.Status);
        Assert.Empty(await _context.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ChangeAccount_SuspendAndActivate_WritesBeforeAndAfterAudit()
    {
        Assert.True((await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("suspend", "Test suspension", true), default)).IsSuccess);
        Assert.True((await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("activate", "Test activation", true), default)).IsSuccess);
        var logs = await _context.AuditLogs.OrderBy(x => x.AuditLogId).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains("Active", logs[0].OldValue);
        Assert.Contains("Suspended", logs[0].NewValue);
        Assert.Equal(1, logs[0].PerformedByUserId);
    }

    [Fact]
    public async Task ChangeAccount_Unlock_ResetsOnlySecurityLock()
    {
        var user = (await _context.Users.FindAsync(2L))!;
        user.LockedUntil = _clock.GetUtcNow().UtcDateTime.AddMinutes(10);
        user.FailedLoginCount = 5;
        await _context.SaveChangesAsync();
        var result = await Admin().ChangeAccountAsync(2,
            new AccountActionRequest("unlock", null, true), default);
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
    public async Task SaveCategory_DuplicateSibling_IsRejected()
    {
        var result = await Admin().SaveCategoryAsync(null,
            new CategoryRequest("Test produce", null, null, 0, true, false), default);
        Assert.Equal("CATEGORY_DUPLICATE", result.Error?.Code);
        Assert.Equal(1, await _context.Categories.CountAsync());
    }

    [Fact]
    public async Task SaveCategory_ParentCycle_IsRejected()
    {
        var result = await Admin().SaveCategoryAsync(1,
            new CategoryRequest("Test produce", 1, null, 0, true, false), default);
        Assert.Equal("CATEGORY_CYCLE", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteCategory_ActiveProduct_IsRejected()
    {
        var result = await Admin().ChangeCategoryAsync(1, "delete", true, default);
        Assert.Equal("CATEGORY_IN_USE", result.Error?.Code);
        Assert.NotNull(await _context.Categories.FindAsync(1L));
    }

    [Fact]
    public async Task DeleteCategory_HistoricalProduct_ArchivesAndRetainsReference()
    {
        (await _context.Products.SingleAsync()).IsPublished = false;
        await _context.SaveChangesAsync();
        Assert.True((await Admin().ChangeCategoryAsync(1, "delete", true, default)).IsSuccess);
        Assert.True((await _context.CategorySettings.SingleAsync()).IsArchived);
        Assert.Equal(1, (await _context.Products.SingleAsync()).CategoryId);
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
        // A real catalog ward (Xã Ngọc Hồi, Hà Nội) outside the configured delivery areas;
        // unknown codes fail earlier with ADDRESS_INVALID.
        var result = await Checkout().QuoteAsync(input with
        {
            Address = input.Address with { WardCode = "523", Ward = "Xã Ngọc Hồi" }
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

    private CheckoutService Checkout(AgriSustainContext? context = null, long userId = 2)
    {
        var database = context ?? _context;
        var policy = new CheckoutPolicy
        {
            PointValueVnd = 1, MaximumPointDiscountPercent = 50,
            Areas = [new DeliveryArea("11", "Thành phố Hà Nội", "", "267",
                "Xã Minh Châu", 10000, 20000, 2, 1)]
        };
        return new CheckoutService(new CheckoutRepository(database),
            new AdministrationRepository(database), new SqlUnitOfWork(database), new TestActor(userId),
            _clock, policy, new VietnamAddressCatalog(), new PolicyShippingRateProvider(policy));
    }

    private static CheckoutRequest Input() => new(new CheckoutAddress("Test Customer",
        "0912345678", "11", "Thành phố Hà Nội", "", "267", "Xã Minh Châu",
        "Test delivery address"), "Standard", "PayOS", null, 0, null);

    private static PlaceOrderRequest Placement(CheckoutQuoteDto quote)
    {
        return new PlaceOrderRequest(quote.Id, Guid.NewGuid().ToString("D"), true);
    }

    private sealed record TestActor(long UserId) : IActorContext;

    private sealed class PolicyShippingRateProvider(CheckoutPolicy policy)
        : IGhtkShippingRateProvider
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
            // Like GHTK, an address outside the served areas is "not supported" (MSG117);
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
