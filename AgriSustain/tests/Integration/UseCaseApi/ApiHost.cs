using System.Collections.Concurrent;
using System.Net.Http.Json;
using AgriSustain.Api;
using AgriSustain.Api.Auth;
using AgriSustain.Api.Controllers;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Application.Services.Promotions;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Auth;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.InMemoryStore;
using AgriSustain.Infrastructure.Email;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using AgriSustain.IntegrationTests.Commerce;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamUnitOfWork = AgriSustain.Application.Interfaces.UnitOfWork.IUnitOfWork;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>
/// HTTP API ↔ service ↔ SQL host for UC-ADM-03/05, UC-RC-02, UC-AUTH-07/08, UC-CS-01, UC-FIN-03, UC-PS-01.
/// Same controllers, services, repositories and middleware order as Program.cs, on a uniquely named
/// SQL Server database. Only the edges are replaced: the test identity header, the clock, the OTP
/// generator, the e-mail sink and the Goship address/rate adapters.
/// </summary>
internal sealed class ApiHost : IAsyncDisposable
{
    public const string ResetEmail = "vana@gmail.com";
    public const string ResetPassword = "OldPass123";
    public static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero);

    private readonly string _database = "AgriSustain_ApiTests_" + Guid.NewGuid().ToString("N");
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "AgriSustain_ApiTests_" + Guid.NewGuid().ToString("N"));
    private string _connection = "";
    private WebApplication _host = null!;

    public MutableClock Clock { get; } = new(T0);
    public EmailSink Email { get; } = new();
    public SequenceOtp Otp { get; } = new();
    public TestShipping Shipping { get; } = new();
    public HttpClient Client { get; private set; } = null!;
    public long AdminId, BuyerId, OtherBuyerId, FarmerUserId, OfficerId, SupportId, FinanceId, PromotionId, ResetUserId;
    public long CategoryId, SpinachId, BatchId, CartItemId, SellerOrderId, SpinachItemId, TicketId, FarmBId;

    public static async Task<ApiHost> StartAsync(bool unreachableGmail = false)
    {
        var host = new ApiHost();
        try
        {
            await host.InitializeAsync(unreachableGmail);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public AgriSustainContext Open() => new(new DbContextOptionsBuilder<AgriSustainContext>()
        .UseSqlServer(_connection).Options);

    public T Service<T>(Func<IServiceProvider, T> resolve, out IServiceScope scope)
    {
        scope = _host.Services.CreateScope();
        return resolve(scope.ServiceProvider);
    }

    public HttpClient As(long userId, string role)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-User");
        Client.DefaultRequestHeaders.Add("X-Test-User", $"{userId}:{role}");
        return Client;
    }

    public HttpClient Anonymous()
    {
        Client.DefaultRequestHeaders.Remove("X-Test-User");
        return Client;
    }

    /// <summary>A separate client so two identities can send requests at the same time.</summary>
    public HttpClient NewClient(long userId, string role)
    {
        var client = _host.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", $"{userId}:{role}");
        return client;
    }

    public HttpClient Admin() => As(AdminId, "SystemAdmin");
    public HttpClient Buyer() => As(BuyerId, "RetailCustomer");
    public HttpClient Support() => As(SupportId, "CustomerSupport");
    public HttpClient Finance() => As(FinanceId, "FinanceStaff");
    public HttpClient Promotion() => As(PromotionId, "PromotionStaff");

    public static CheckoutRequest CheckoutInput(string phone = "0912345678", string? voucher = null, int points = 0) =>
        new(new CheckoutAddress("Test Customer", phone, "100000", "Hà Nội", "100300", "Quận Hai Bà Trưng",
            "19", "Phường Quỳnh Lôi", "Test delivery address"), "Standard", "PayOS", voucher, points, null);

    public async Task<CheckoutQuoteDto> QuoteAsync(CheckoutRequest? input = null)
    {
        var response = await Buyer().PostAsJsonAsync("api/checkout/quotes", input ?? CheckoutInput());
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Quote failed {(int)response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<CheckoutQuoteDto>())!;
    }

    private async Task InitializeAsync(bool unreachableGmail)
    {
        var server = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_SERVER");
        var configured = Environment.GetEnvironmentVariable("AGRISUSTAIN_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Missing test SQL Server configuration.");
        }

        var settings = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder { DataSource = server, IntegratedSecurity = true, TrustServerCertificate = true }
            : new SqlConnectionStringBuilder(configured);
        // All operations target this unique test catalog, never an application database.
        settings.InitialCatalog = _database;
        settings.ConnectTimeout = 30;
        settings.AttachDBFilename = "";
        _connection = settings.ConnectionString;

        await using (var db = Open())
        {
            await db.Database.EnsureCreatedAsync();
        }

        Directory.CreateDirectory(_contentRoot);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing", ContentRootPath = _contentRoot
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MyCnn"] = _connection,
            ["Goship:BaseUrl"] = "http://127.0.0.1/",
            ["Goship:Enabled"] = "false",
            ["Checkout:QuoteMinutes"] = "5",
            ["Checkout:ReservationMinutes"] = "15",
            ["Checkout:PointValueVnd"] = "1",
            ["Checkout:MaximumPointDiscountPercent"] = "50",
            ["JWT:ValidIssuer"] = "agrisustain-tests",
            ["JWT:ValidAudience"] = "agrisustain-tests",
            ["JWT:Secret"] = "integration-tests-only-secret-key-0123456789",
            ["JWT:ExpiryMinutes"] = "60",
        });
        RegisterServices(builder, unreachableGmail);

        _host = builder.Build();
        // Same order as Program.cs.
        _host.UseMiddleware<FeatureExceptionMiddleware>();
        _host.UseWhen(context => context.Request.Path.StartsWithSegments("/api/users")
            || context.Request.Path.StartsWithSegments("/api/categories")
            || context.Request.Path.StartsWithSegments("/api/checkout"), branch =>
                branch.UseMiddleware<ExceptionMiddleware>());
        _host.UseAuthentication();
        _host.UseAuthorization();
        _host.UseMiddleware<ActiveFeatureAccountMiddleware>();
        _host.MapControllers();
        await _host.StartAsync();
        Client = _host.GetTestClient();

        await SeedAsync();
    }

    private void RegisterServices(WebApplicationBuilder builder, bool unreachableGmail)
    {
        var services = builder.Services;
        services.AddThreeUseCases(builder.Configuration);
        services.AddSingleton<RevokedTokenStore>();
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()))
            .AddApplicationPart(typeof(AuthController).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<ComplaintResolutionService>();
        services.AddScoped<IComplaintResolutionService>(sp => sp.GetRequiredService<ComplaintResolutionService>());
        services.AddScoped<IComplaintEvidenceTimeoutProcessor>(sp => sp.GetRequiredService<ComplaintResolutionService>());
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IRefundProcessingService, RefundProcessingService>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IVoucherManagementService, VoucherManagementService>();
        services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddSingleton<SqlDemoDependencies>();
        services.AddScoped<ICurrentUser, CurrentActor>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<TeamUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<AgriSustain.Application.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ICurrentActor, CurrentActor>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();

        if (unreachableGmail)
        {
            services.AddSingleton(Options.Create(new GmailOptions
            {
                Host = "127.0.0.1", Port = 1, SenderAddress = "sender@example.test",
                AppPassword = "wrong app password", TimeoutSeconds = 5
            }));
            services.AddSingleton<IEmailService, GmailEmailService>();
        }
        else
        {
            services.AddSingleton<IEmailService>(Email);
        }

        services.AddSingleton<IOtpGenerator>(Otp);
        services.AddSingleton<IOtpChallengeStore, InMemoryOtpChallengeStore>();
        services.AddScoped<IEmailOtpVerifier, EmailOtpVerifier>();
        services.AddScoped<AgriSustain.Application.Interfaces.Services.IAuthenticationService,
            AgriSustain.Application.Services.Authentication.AuthenticationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });

        // Edges replaced last so they win over the production registrations.
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IAddressCatalog, TestAddressCatalog>();
        services.AddSingleton<IShippingRateProvider>(Shipping);
    }

    private async Task SeedAsync()
    {
        var now = T0.UtcDateTime;
        await using var db = Open();
        User NewUser(string name, string role, string? email = null) => new()
        {
            Username = name, Email = email ?? $"{name}@example.test", FullName = name, PasswordHash = "test-only",
            Role = role, Status = "Active", IsEmailVerified = true, CreatedAt = now
        };
        var admin = NewUser("admin", "SystemAdmin");
        var buyer = NewUser("buyer", "RetailCustomer");
        var otherBuyer = NewUser("buyer2", "RetailCustomer");
        var farmerA = NewUser("farmer-a", "Farmer");
        var farmerB = NewUser("farmer-b", "Farmer");
        var officer = NewUser("officer", "VerificationOfficer");
        var support = NewUser("support", "CustomerSupport");
        var finance = NewUser("finance", "FinanceStaff");
        var promotion = NewUser("promotion", "PromotionStaff");
        var resetUser = NewUser("vana", "RetailCustomer", ResetEmail);
        db.Users.AddRange(admin, buyer, otherBuyer, farmerA, farmerB, officer, support, finance, promotion, resetUser);
        await db.SaveChangesAsync();
        (AdminId, BuyerId, OtherBuyerId, FarmerUserId, OfficerId) = (admin.UserId, buyer.UserId, otherBuyer.UserId, farmerA.UserId, officer.UserId);
        (SupportId, FinanceId, PromotionId, ResetUserId) = (support.UserId, finance.UserId, promotion.UserId, resetUser.UserId);
        // UC-CS-01 snapshots the buyer's default account onto the refund for UC-FIN-03.
        db.SupportedBanks.Add(new SupportedBank { BankCode = "VCB", ShortName = "Vietcombank", FullName = "Test bank", IsActive = true });
        db.UserBankAccounts.AddRange(
            new UserBankAccount { UserId = buyer.UserId, BankCode = "VCB", AccountNumber = "0071000111111", AccountHolder = "BUYER", IsDefault = true, CreatedAt = now },
            new UserBankAccount { UserId = otherBuyer.UserId, BankCode = "VCB", AccountNumber = "0071000222222", AccountHolder = "BUYER TWO", IsDefault = true, CreatedAt = now });
        await db.SaveChangesAsync();

        FarmerProfile Farm(User user) => new()
        {
            UserId = user.UserId, FarmName = $"{user.Username} farm", RepresentativeName = user.FullName,
            Address = "Test address", ProvinceCode = "TEST", VerificationStatus = "Approved"
        };
        var category = new Category { CategoryName = "Test produce", IsActive = true };
        var farmA = Farm(farmerA);
        var farmB = Farm(farmerB);
        db.AddRange(category, farmA, farmB);
        await db.SaveChangesAsync();
        (CategoryId, FarmBId) = (category.CategoryId, farmB.FarmerProfileId);

        Product NewProduct(FarmerProfile farm, string name, decimal price) => new()
        {
            CategoryId = category.CategoryId, FarmerProfileId = farm.FarmerProfileId, ProductName = name,
            Unit = "kg", BaseRetailPrice = price, ApprovalStatus = "Approved", IsPublished = true, CreatedAt = now
        };
        var spinach = NewProduct(farmA, "Cải bó xôi", 15_000);
        var tomato = NewProduct(farmA, "Cà chua", 10_000);
        var rice = NewProduct(farmB, "Gạo", 20_000);
        db.Products.AddRange(spinach, tomato, rice);
        await db.SaveChangesAsync();
        SpinachId = spinach.ProductId;

        var season = new CropSeason
        {
            FarmerProfileId = farmA.FarmerProfileId, ProductId = spinach.ProductId, SeasonName = "Test season",
            Status = "Harvesting", CropVariety = "Test variety", YieldUnit = "kg"
        };
        db.CropSeasons.Add(season);
        db.ProductImages.Add(new ProductImage { ProductId = spinach.ProductId, ImageUrl = "https://example.test/spinach.png", IsPrimary = true });
        await db.SaveChangesAsync();
        var batch = new HarvestBatch
        {
            ProductId = spinach.ProductId, CropSeasonId = season.CropSeasonId, BatchCode = "SPINACH-01",
            Status = "Available", ActualHarvestDate = DateOnly.FromDateTime(now.AddDays(-1)), StockEnteredAt = now,
            HarvestYield = 20, AvailableQuantity = 20, ExpirationDate = DateOnly.FromDateTime(now.AddDays(10))
        };
        db.HarvestBatches.Add(batch);
        var cart = new Cart { UserId = buyer.UserId, UpdatedAt = now };
        var cartItem = new CartItem { ProductId = spinach.ProductId, Quantity = 2, IsSelected = true, AddedAt = now };
        cart.CartItems.Add(cartItem);
        db.Carts.Add(cart);
        db.RewardPointEntries.Add(new RewardPointEntry
        {
            UserId = buyer.UserId, EntryType = "Earn", Points = 1000, RequestKey = "seed-earn", OccurredAt = now
        });
        await db.SaveChangesAsync();
        (BatchId, CartItemId) = (batch.HarvestBatchId, cartItem.CartItemId);

        // Delivered order: seller order A 180 000 ₫ (after 20 000 ₫ voucher share) + seller order B 100 000 ₫.
        var order = new Order
        {
            BuyerUserId = otherBuyer.UserId, OrderCode = "RC-COMPLAINT", OrderType = "Retail", OrderDate = now.AddDays(-2),
            ShippingAddress = "Test address", OrderStatus = "Delivered", RecipientNameSnapshot = "Buyer",
            RecipientPhoneSnapshot = "0912345678", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
            TotalAmount = 300_000, DiscountAmount = 20_000, FinalAmount = 280_000
        };
        var sellerA = new SellerOrder
        {
            FarmerProfileId = farmA.FarmerProfileId, SellerOrderCode = "SO-A", Status = "Delivered",
            Subtotal = 200_000, DiscountShare = 20_000, FinalAmount = 180_000, DeliveredAt = now.AddHours(-2)
        };
        sellerA.OrderItems.Add(new OrderItem
        {
            ProductId = spinach.ProductId, ProductNameSnapshot = "Cải bó xôi", Quantity = 10, UnitSnapshot = "kg",
            UnitPriceSnapshot = 15_000, SubTotal = 150_000, ItemStatus = "Delivered"
        });
        sellerA.OrderItems.Add(new OrderItem
        {
            ProductId = tomato.ProductId, ProductNameSnapshot = "Cà chua", Quantity = 5, UnitSnapshot = "kg",
            UnitPriceSnapshot = 10_000, SubTotal = 50_000, ItemStatus = "Delivered"
        });
        var sellerB = new SellerOrder
        {
            FarmerProfileId = farmB.FarmerProfileId, SellerOrderCode = "SO-B", Status = "Delivered",
            Subtotal = 100_000, FinalAmount = 100_000, DeliveredAt = now.AddHours(-2)
        };
        sellerB.OrderItems.Add(new OrderItem
        {
            ProductId = rice.ProductId, ProductNameSnapshot = "Gạo", Quantity = 5, UnitSnapshot = "kg",
            UnitPriceSnapshot = 20_000, SubTotal = 100_000, ItemStatus = "Delivered"
        });
        order.SellerOrders.Add(sellerA);
        order.SellerOrders.Add(sellerB);
        order.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = "PAY-COMPLAINT", GatewayProvider = "PayOS", Amount = 280_000, PaymentType = "VietQR",
            PaymentStatus = "Success", PaymentPurpose = "Order", RequestedAt = now.AddDays(-2), PaidAt = now.AddDays(-2),
            RequestKey = "pay-complaint"
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        SellerOrderId = sellerA.SellerOrderId;
        SpinachItemId = sellerA.OrderItems.Single(item => item.ProductNameSnapshot == "Cải bó xôi").OrderItemId;

        var ticket = new DisputeTicket
        {
            SubmittedByUserId = otherBuyer.UserId, SellerOrderId = sellerA.SellerOrderId, DisputeReason = "Damaged",
            TicketStatus = DisputeTicket.StatusOpen, CreatedAt = now.AddHours(-1)
        };
        db.DisputeTickets.Add(ticket);
        await db.SaveChangesAsync();
        TicketId = ticket.DisputeTicketId;

        // The reset user gets a real hash from the application's password service.
        using var scope = _host.Services.CreateScope();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var tracked = await db.Users.SingleAsync(user => user.UserId == resetUser.UserId);
        tracked.PasswordHash = passwords.Hash(tracked, ResetPassword);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (_connection.Length > 0)
        {
            try
            {
                await using var db = Open();
                await db.Database.EnsureDeletedAsync();
            }
            catch (SqlException)
            {
                // A leftover uniquely named test database is harmless.
            }
        }

        try { Directory.Delete(_contentRoot, true); } catch (IOException) { }
    }
}

internal sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class EmailSink : IEmailService
{
    public bool Succeeds { get; set; } = true;
    public ConcurrentQueue<(string Email, string Otp, string Purpose)> Sent { get; } = new();

    public Task<bool> SendOtpEmailAsync(string email, string otpCode, string purpose, CancellationToken cancellationToken)
    {
        if (Succeeds)
        {
            Sent.Enqueue((email, otpCode, purpose));
        }

        return Task.FromResult(Succeeds);
    }
}

internal sealed class SequenceOtp : IOtpGenerator
{
    private readonly ConcurrentQueue<string> _codes = new();
    public void Next(params string[] codes) { foreach (string code in codes) _codes.Enqueue(code); }
    public string Generate() => _codes.TryDequeue(out var code) ? code : "482913";
}

internal sealed class TestShipping : IShippingRateProvider
{
    public bool Down { get; set; }
    public int EstimatedDeliveryDays(string deliveryMethod) => 2;
    public Task<ShippingRateResult?> GetRateAsync(ShippingRateRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Down ? null : new ShippingRateResult(true, 20_000m));
}

/// <summary>Offline stand-in for the Goship province/district/ward catalog.</summary>
internal sealed class TestAddressCatalog : IAddressCatalog
{
    public Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProvinceOption>>([new("100000", "Hà Nội")]);
    public Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DistrictOption>>([new("100300", "100000", "Quận Hai Bà Trưng")]);
    public Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WardOption>>([new("19", "100300", "Phường Quỳnh Lôi")]);
    public Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress address, CancellationToken cancellationToken) =>
        Task.FromResult(address is { ProvinceCode: "100000", DistrictCode: "100300", WardCode: "19" }
            ? address with { Province = "Hà Nội", District = "Quận Hai Bà Trưng", Ward = "Phường Quỳnh Lôi", CatalogVersion = "test" }
            : null);
}
