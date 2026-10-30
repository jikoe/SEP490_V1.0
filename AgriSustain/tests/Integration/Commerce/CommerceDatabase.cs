using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using AgriSustain.Api.Controllers;

using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

// Opt in explicitly: this suite creates/deletes only a uniquely named test database.
public sealed class CommerceSqlFactAttribute : FactAttribute
{
    public CommerceSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_CONNECTION")))
            Skip = "Set AGRISUSTAIN_TEST_SQL_SERVER to an isolated SQL Server with CREATE DATABASE permission.";
    }
}
public sealed class CommerceSqlTheoryAttribute : TheoryAttribute
{
    public CommerceSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_CONNECTION")))
            Skip = "Set AGRISUSTAIN_TEST_SQL_SERVER to an isolated SQL Server with CREATE DATABASE permission.";
    }
}

internal sealed class TestClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);
}
internal sealed class FailNextSave : SaveChangesInterceptor
{
    public bool Armed;
    // Throw after SQL writes succeed but before UnitOfWork commits its transaction.
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (Armed) { Armed = false; throw new IOException("Test-only persistence failure"); }
        return ValueTask.FromResult(result);
    }
}
// This authentication scheme exists ONLY in this test assembly/TestServer.
internal sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var header))
            return Task.FromResult(AuthenticateResult.NoResult());
        var fields = header.ToString().Split(':');
        if (fields.Length != 2 || !long.TryParse(fields[0], out _))
            return Task.FromResult(AuthenticateResult.Fail("Invalid test identity"));
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, fields[0]),
            new(ClaimTypes.Role, fields[1])], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

internal sealed class CommerceDatabase : IAsyncDisposable
{
    private const string Prefix = "AgriSustain_CommerceTests_";
    private readonly string databaseName = Prefix + Guid.NewGuid().ToString("N");
    private string Connection = "";
    private bool created;
    public readonly TestClock Clock = new();
    public readonly FailNextSave Failure = new();
    private readonly string filesRoot = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
    public long FarmerId, BuyerId, OtherBuyerId, ProductId, BatchId, SellerId, CartItemId, CategoryId;
    public WebApplication Host = null!;
    public HttpClient Client = null!;
    public DbContextOptions<AgriSustainContext> Options => new DbContextOptionsBuilder<AgriSustainContext>()
        .UseSqlServer(Connection, sql => sql.CommandTimeout(30)).AddInterceptors(Failure).Options;
    public AgriSustainContext Open() => new(Options);

    public static async Task<CommerceDatabase> Create()
    {
        var fixture = new CommerceDatabase();
        try { await fixture.Initialize(); return fixture; }
        catch (Exception original)
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException("Test setup and cleanup failed.", original, cleanup); }
            throw;
        }
    }
    private async Task Initialize()
    {
        var server = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER");
        var configured = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Missing test SQL Server configuration.");
        var settings = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder { DataSource = server, IntegratedSecurity = true, TrustServerCertificate = true }
            : new SqlConnectionStringBuilder(configured);
        // Never use a configured application's catalog; all operations target this unique test catalog.
        settings.InitialCatalog = databaseName; settings.ConnectTimeout = 5; settings.Pooling = false;
        settings.AttachDBFilename = "";
        Connection = settings.ConnectionString;
        var probe = new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" };
        await using (var connection = new SqlConnection(probe.ConnectionString))
            await connection.OpenAsync();
        await using (var db = Open())
        {
            created = true; // EnsureDeleted also cleans up a partially created test schema.
            await db.Database.EnsureCreatedAsync();
            User User(string name, string role) => new()
            {
                Username = name,
                Email = name + "@example.test",
                FullName = name,
                PasswordHash = "test-only",
                Role = role,
                Status = "Active",
                IsEmailVerified = true,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            var farmerUser = User("farmer", "Farmer");
            var buyer = User("buyer", "RetailCustomer");
            var otherBuyer = User("other", "RetailCustomer");
            var support = User("support", "CustomerSupport");
            db.Users.AddRange(farmerUser, buyer, otherBuyer, support);
            await db.SaveChangesAsync();
            FarmerId = farmerUser.UserId; BuyerId = buyer.UserId; OtherBuyerId = otherBuyer.UserId;
            var imageDirectory = Path.Combine(filesRoot, "wwwroot", "media", FarmerId.ToString());
            var evidenceDirectory = Path.Combine(filesRoot, "private-files", FarmerId.ToString());
            Directory.CreateDirectory(imageDirectory); Directory.CreateDirectory(evidenceDirectory);
            await File.WriteAllBytesAsync(Path.Combine(imageDirectory, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg"), [255, 216, 255, 217]);
            await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"), "%PDF-1.4 test evidence");
            var farm = new FarmerProfile
            {
                UserId = FarmerId,
                FarmName = "Test farm",
                Address = "Test address",
                ProvinceCode = "TEST",
                RepresentativeName = "Test farmer",
                VerificationStatus = "Approved"
            };
            var category = new Category { CategoryName = "Vegetables", IsActive = true };
            db.AddRange(farm, category);
            await db.SaveChangesAsync();
            CategoryId = category.CategoryId;
            var product = new Product
            {
                FarmerProfileId = farm.FarmerProfileId,
                CategoryId = CategoryId,
                ProductName = "Tomato",
                Description = "Fresh tomato",
                Unit = "kg",
                BaseRetailPrice = 30000,
                WholesaleMinQuantity = 10,
                WholesalePrice = 25000,
                ApprovalStatus = "Approved",
                IsPublished = true,
                CreatedAt = Clock.GetUtcNow().UtcDateTime
            };
            product.ProductImages.Add(new ProductImage { ImageUrl = Image, IsPrimary = true });
            db.Products.Add(product); await db.SaveChangesAsync(); ProductId = product.ProductId;
            var season = new CropSeason
            {
                FarmerProfileId = farm.FarmerProfileId,
                ProductId = ProductId,
                SeasonName = "Test season",
                CropVariety = "Test variety",
                Status = "Harvesting",
                YieldUnit = "kg",
                PlantingDate = new(2026, 1, 1),
                EstimatedHarvestDate = new(2026, 10, 25)
            };
            db.CropSeasons.Add(season); await db.SaveChangesAsync();
            var batch = new HarvestBatch
            {
                ProductId = ProductId,
                CropSeasonId = season.CropSeasonId,
                BatchCode = "TEST-BATCH",
                Status = "Available",
                AvailableQuantity = 20,
                ReservedQuantity = 5,
                HarvestYield = 25,
                ActualHarvestDate = new(2026, 10, 26),
                ExpirationDate = new(2026, 11, 6),
                StockEnteredAt = Clock.GetUtcNow().UtcDateTime
            };
            db.HarvestBatches.Add(batch); await db.SaveChangesAsync(); BatchId = batch.HarvestBatchId;
            var cart = new Cart { UserId = BuyerId, UpdatedAt = Clock.GetUtcNow().UtcDateTime };
            var cartItem = new CartItem
            {
                ProductId = ProductId,
                HarvestBatchId = BatchId,
                Quantity = 2,
                IsSelected = true,
                AddedAt = Clock.GetUtcNow().UtcDateTime
            };
            cart.CartItems.Add(cartItem); db.Carts.Add(cart);
            var order = new Order
            {
                BuyerUserId = BuyerId,
                OrderCode = "TEST-ORDER",
                OrderType = "Retail",
                OrderStatus = "Processing",
                OrderDate = Clock.GetUtcNow().UtcDateTime,
                ShippingAddress = "Test shipping address",
                RecipientNameSnapshot = "Buyer",
                RecipientPhoneSnapshot = "0912345678",
                TotalAmount = 150000,
                DiscountAmount = 10000,
                DeliveryFee = 20000,
                FinalAmount = 160000,
                VoucherCodeSnapshot = "TEST10",
                VoucherDiscount = 10000,
                PointDiscount = 0,
                CheckoutRequestKey = Guid.NewGuid().ToString("D")
            };
            var seller = new SellerOrder
            {
                FarmerProfileId = farm.FarmerProfileId,
                SellerOrderCode = "TEST-SELLER",
                Subtotal = 150000,
                DiscountShare = 10000,
                DeliveryFee = 20000,
                FinalAmount = 160000,
                Status = "Pending"
            };
            var line = new OrderItem
            {
                ProductId = ProductId,
                ProductNameSnapshot = "Tomato",
                UnitSnapshot = "kg",
                Quantity = 5,
                UnitPriceSnapshot = 30000,
                SubTotal = 150000
            };
            line.OrderItemBatchAllocations.Add(new OrderItemBatchAllocation
            {
                HarvestBatchId = BatchId,
                AllocationSequence = 1,
                AllocatedQuantity = 5,
                AllocationStatus = "Reserved",
                ReservedAt = Clock.GetUtcNow().UtcDateTime,
                ExpiresAt = Clock.GetUtcNow().AddDays(1).UtcDateTime
            });
            seller.OrderItems.Add(line); order.SellerOrders.Add(seller);
            order.PaymentTransactions.Add(new PaymentTransaction
            {
                TransactionCode = "TEST-PAY",
                GatewayProvider = "Test",
                Amount = 160000,
                PaymentType = "Full",
                PaymentStatus = "Success",
                PaymentPurpose = "RetailOrder",
                RequestedAt = Clock.GetUtcNow().UtcDateTime,
                PaidAt = Clock.GetUtcNow().UtcDateTime,
                RequestKey = Guid.NewGuid().ToString("D")
            });
            db.Orders.Add(order); await db.SaveChangesAsync();
            SellerId = seller.SellerOrderId; CartItemId = cartItem.CartItemId;
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = filesRoot });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.AddSingleton(Failure);
        builder.Services.AddDbContext<AgriSustainContext>(options => options.UseSqlServer(Connection)
            .AddInterceptors(Failure));
        builder.Services.AddScoped<IFileStorageService, FileStorageService>();
        builder.Services.AddSingleton(new SqlDemoDependencies());
        builder.Services.AddScoped<AgriSustain.Application.Interfaces.IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<ICartRepository, CartRepository>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<FarmerProductService>();
        builder.Services.AddScoped<RetailCartService>();
        builder.Services.AddScoped<OrderFulfillmentService>();
        builder.Services.AddSingleton<IValidator<ProductRequest>, ProductRequestValidator>();
        builder.Services.AddSingleton<IValidator<ProductQuery>, ProductQueryValidator>();
        builder.Services.AddSingleton<ProductRequestValidator>();
        builder.Services.AddSingleton<ProductQueryValidator>();

        builder.Services.Configure<QuantityOptions>(_ => { });
        builder.Services.AddScoped<CartPricingService>();
        builder.Services.AddScoped<FulfillmentRules>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor, CurrentActor>();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(FarmerProductController).Assembly);
        Host = builder.Build();
        Host.UseMiddleware<FeatureExceptionMiddleware>();
        Host.UseAuthentication();
        Host.UseMiddleware<ActiveFeatureAccountMiddleware>();
        Host.UseAuthorization();
        Host.MapControllers();
        await Host.StartAsync();
        Client = Host.GetTestClient();
    }
    public string Image => $"/media/{FarmerId}/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";
    public ProductRequest ProductRequest(string? version = null) => new("Tomato", CategoryId, "kg", "Fresh tomato",
        30000, 10, 25000, [new(Image, true, null)], version);
    public HttpClient As(long user, string role)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-User");
        Client.DefaultRequestHeaders.Add("X-Test-User", $"{user}:{role}");
        return Client;
    }
    public HttpClient Farmer() => As(FarmerId, "Farmer");
    public HttpClient Buyer() => As(BuyerId, "RetailCustomer");
    public async Task Edit(Func<AgriSustainContext, Task> action)
    {
        await using var db = Open(); await action(db); await db.SaveChangesAsync();
    }
    public async Task<OrderActionRequest> Action(string? reason = null, string? explanation = null, string[]? evidence = null)
    {
        var order = await Farmer().GetFromJsonAsync<SellerOrderResponse>($"api/farmer/orders/{SellerId}");
        return new(order!.Version, Guid.NewGuid().ToString("D"), reason, explanation, evidence);
    }
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Host is not null) await Host.DisposeAsync();
        if (Directory.Exists(filesRoot))
        {
            var fullPath = Path.GetFullPath(filesRoot);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullPath).StartsWith(Prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove non-test files.");
            Directory.Delete(fullPath, recursive: true);
        }
        if (!created) return;
        var parsed = new SqlConnectionStringBuilder(Connection);
        if (parsed.InitialCatalog != databaseName || !databaseName.StartsWith(Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete a non-test database.");
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }
}
