using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriSustain.Api;
using AgriSustain.Api.Auth;
using AgriSustain.Api.Controllers;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Auth;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.InMemoryStore;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
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
using TeamUnitOfWork = AgriSustain.Application.Interfaces.UnitOfWork.IUnitOfWork;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>
/// DuND integration host (Report 5.2): HTTP API -> controller -> service -> repository -> SQL Server for
/// UC-AUTH-01, UC-AUTH-02, UC-AUTH-03, UC-RC-04, UC-CM-02, UC-FAR-07, UC-FIN-06, UC-FIN-08, UC-TRC-01,
/// UC-WB-01 and UC-WB-08. Registrations follow Program.cs ("DuND" blocks). Only the edges are replaced:
/// test identity header, clock, OTP generator, Gmail sink, Goship address catalog and bank-change e-mail.
/// Each test class instance gets its own uniquely named database, deleted on dispose.
/// </summary>
internal sealed class DuApiHost : IAsyncDisposable
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 27, 1, 0, 0, TimeSpan.Zero); // 08:00 in Viet Nam
    public const string WebhookApiKey = "du-it-webhook-key";
    public const string SePayAccount = "0000499456980";
    public const string Otp = "482913";

    private readonly string _database = "AgriSustain_DuIT_" + Guid.NewGuid().ToString("N");
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "AgriSustain_DuIT_" + Guid.NewGuid().ToString("N"));
    private string _connection = "";
    private WebApplication _host = null!;

    public MutableClock Clock { get; } = new(T0);
    public EmailSink Email { get; } = new();
    public SequenceOtp OtpCodes { get; } = new();
    public MailSink Mail { get; } = new();
    public BankChangeMailSink BankMail { get; } = new();
    public HttpClient Client { get; private set; } = null!;
    public string ContentRoot => _contentRoot;

    // Seeded identities and records (see SeedAsync).
    public long CustomerId, Customer2Id, FarmerAId, FarmerBId, FarmerNewId, WholesaleId, WholesaleNewId;
    public long OfficerId, ModeratorId, FinanceId;
    public long FarmAId, FarmBId, FarmNewId, WholesaleProfileId, CategoryId, AvocadoId;
    public long DeliveredOrderId, CompletedOrderId, DisputedOrderId, ProcessingOrderId, OtherOrderId;
    public long CleanReviewId, FlaggedReviewId, HiddenReviewId;
    public long PendingRefundId, CompletedRefundId, OtherRefundId;
    public long FreeTierId, SilverTierId, GoldTierId;
    public long ContractId, OverdueLineId, FutureLineId, TerminatedContractId, TerminatedLineId;

    public static async Task<DuApiHost> StartAsync()
    {
        var host = new DuApiHost();
        try
        {
            await host.InitializeAsync();
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

    public HttpClient Customer() => As(CustomerId, "RetailCustomer");
    public HttpClient FarmerA() => As(FarmerAId, "Farmer");
    public HttpClient FarmerNew() => As(FarmerNewId, "Farmer");
    public HttpClient Wholesale() => As(WholesaleId, "WholesaleBuyer");
    public HttpClient WholesaleNew() => As(WholesaleNewId, "WholesaleBuyer");
    public HttpClient Moderator() => As(ModeratorId, "ContentModerator");
    public HttpClient Finance() => As(FinanceId, "FinanceStaff");

    /// <summary>Minimal valid PDF bytes (the dossier check reads the "%PDF-" signature).</summary>
    public static ByteArrayContent Pdf(string fileName = "license.pdf")
    {
        var content = new ByteArrayContent("%PDF-1.4\n%test document\n"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return content;
    }

    /// <summary>Sends a SePay "in" transfer to the webhook exactly as SePay posts it.</summary>
    public async Task<HttpResponseMessage> SePayTransferAsync(long id, decimal amount, string content,
        string transactionDate = "2026-10-27 08:05:00")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = JsonContent.Create(new
            {
                id, gateway = "MBBank", transactionDate, accountNumber = SePayAccount, subAccount = "",
                code = (string?)null, content, transferType = "in",
                description = "BankAPINotify " + content, transferAmount = amount, accumulated = 10_000_000,
                referenceCode = $"FT{id}"
            })
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Apikey {WebhookApiKey}");
        return await Anonymous().SendAsync(request);
    }

    private async Task InitializeAsync()
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
        // Every statement targets this unique test catalog, never an application database.
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
            ["Support:Email"] = "support@agrisustain.test",
            ["Checkout:QuoteMinutes"] = "5",
            ["Checkout:ReservationMinutes"] = "15",
            ["Checkout:PointValueVnd"] = "1",
            ["Checkout:MaximumPointDiscountPercent"] = "50",
            ["JWT:ValidIssuer"] = "agrisustain-tests",
            ["JWT:ValidAudience"] = "agrisustain-tests",
            ["JWT:Secret"] = "integration-tests-only-secret-key-0123456789",
            ["JWT:ExpiryMinutes"] = "60",
            // SePay receiving account (VietQR for UC-WB-08, webhook account check for UC-FIN-06).
            ["Payment:SePay:AccountNumber"] = SePayAccount,
            ["Payment:SePay:BankShortName"] = "MBBank",
            ["Payment:SePay:BankName"] = "MB Bank",
            ["Payment:SePay:AccountName"] = "AGRISUSTAIN",
            ["Payment:SePay:WebhookApiKey"] = WebhookApiKey,
        });
        RegisterServices(builder);

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

    private void RegisterServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddThreeUseCases(builder.Configuration);
        services.AddSingleton<RevokedTokenStore>();
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()))
            .AddApplicationPart(typeof(AuthController).Assembly);

        // Team services the shared controllers need (same as ApiHost / Program.cs).
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<SqlDemoDependencies>();
        services.AddScoped<ICurrentUser, CurrentActor>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<TeamUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<AgriSustain.Application.Interfaces.IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ICurrentActor, CurrentActor>();
        services.AddScoped<IFileStorageService, AgriSustain.Infrastructure.FileStorage.FileStorageService>();
        services.AddScoped<AgriSustain.Infrastructure.Payments.BankTransferReconciler>();
        services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();

        // DuND: authentication and e-mail OTP (UC-AUTH-01 / UC-AUTH-08).
        services.AddSingleton<IEmailService>(Email);
        services.AddSingleton<IOtpGenerator>(OtpCodes);
        services.AddSingleton<IOtpChallengeStore, InMemoryOtpChallengeStore>();
        services.AddScoped<IEmailOtpVerifier, EmailOtpVerifier>();
        services.AddScoped<AgriSustain.Application.Interfaces.Services.IAuthenticationService,
            AgriSustain.Application.Services.Authentication.AuthenticationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        // DuND: UC-AUTH-03 profile and bank account tab.
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IBankAccountRepository, BankAccountRepository>();
        services.AddScoped<IBankAccountLookup, AgriSustain.Infrastructure.Banking.FakeBankAccountLookup>();
        services.AddSingleton<IBankAccountEmailSender>(BankMail);
        services.AddScoped<IBankAccountService, BankAccountService>();

        // DuND ITE2: UC-AUTH-02, UC-FAR-07, UC-CM-02, UC-TRC-01, UC-FIN-08.
        services.AddSingleton<AgriSustain.Infrastructure.FileStorage.DossierStorage>();
        services.AddSingleton<IDossierStorage>(sp =>
            sp.GetRequiredService<AgriSustain.Infrastructure.FileStorage.DossierStorage>());
        services.AddScoped<AgriSustain.Application.Services.BusinessProfiles.IBusinessProfileRepository, BusinessProfileRepository>();
        services.AddScoped<AgriSustain.Application.Services.BusinessProfiles.IBusinessProfileService,
            AgriSustain.Application.Services.BusinessProfiles.BusinessProfileService>();
        services.AddScoped<AgriSustain.Application.Services.Certificates.IFarmCertificateRepository, FarmCertificateRepository>();
        services.AddScoped<AgriSustain.Application.Services.Certificates.IFarmCertificateService,
            AgriSustain.Application.Services.Certificates.FarmCertificateService>();
        services.AddScoped<AgriSustain.Application.Services.Moderation.IReviewModerationRepository, ReviewModerationRepository>();
        services.AddScoped<AgriSustain.Application.Services.Moderation.IReviewModerationService,
            AgriSustain.Application.Services.Moderation.ReviewModerationService>();
        services.AddScoped<AgriSustain.Application.Services.Traceability.ITraceabilityRepository, TraceabilityRepository>();
        services.AddScoped<AgriSustain.Application.Services.Traceability.ITraceabilityService,
            AgriSustain.Application.Services.Traceability.TraceabilityService>();
        services.AddScoped<AgriSustain.Application.Services.Refunds.IRefundAccountRepository, RefundAccountRepository>();
        services.AddScoped<AgriSustain.Application.Services.Refunds.IRefundAccountService,
            AgriSustain.Application.Services.Refunds.RefundAccountService>();

        // DuND ITE3: UC-WB-01, UC-WB-08, UC-FIN-06 (SePay handlers in the same order as Program.cs).
        services.AddScoped<AgriSustain.Application.Services.B2B.ISupplierDirectoryRepository, SupplierDirectoryRepository>();
        services.AddScoped<AgriSustain.Application.Services.B2B.ISupplierDirectoryService,
            AgriSustain.Application.Services.B2B.SupplierDirectoryService>();
        services.AddScoped<AgriSustain.Application.Services.B2B.IBuyerSubscriptionRepository, BuyerSubscriptionRepository>();
        services.AddScoped<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>();
        services.AddScoped<AgriSustain.Application.Services.B2B.IBuyerSubscriptionService>(sp =>
            sp.GetRequiredService<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>());
        services.AddSingleton<IPaymentQrProvider, AgriSustain.Infrastructure.Payments.SePayQrProvider>();
        services.AddScoped<AgriSustain.Application.Services.Finance.IContractPaymentRepository, ContractPaymentRepository>();
        services.AddScoped<AgriSustain.Application.Services.Finance.ContractPaymentService>();
        services.AddScoped<AgriSustain.Application.Services.Finance.IContractPaymentService>(sp =>
            sp.GetRequiredService<AgriSustain.Application.Services.Finance.ContractPaymentService>());
        services.AddScoped<IBankTransferHandler>(sp =>
            sp.GetRequiredService<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>());
        services.AddScoped<IBankTransferHandler>(sp =>
            sp.GetRequiredService<AgriSustain.Application.Services.Finance.ContractPaymentService>());
        services.AddScoped<IOrderTrackingService, OrderTrackingService>();
        services.AddSingleton<INotificationGateway>(Mail);

        services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });

        // Edges replaced last so they win over the production registrations.
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IAddressCatalog, TestAddressCatalog>();
    }

    private async Task SeedAsync()
    {
        var now = T0.UtcDateTime;
        await using var db = Open();
        User NewUser(string name, string role, string? phone = null) => new()
        {
            Username = name, Email = $"{name}@example.test", FullName = name, PasswordHash = "test-only",
            PhoneNumber = phone, Role = role, Status = "Active", IsEmailVerified = true, CreatedAt = now.AddDays(-30)
        };
        var customer = NewUser("customer", "RetailCustomer", "0912000001");
        var customer2 = NewUser("customer2", "RetailCustomer", "0912000002");
        var farmerA = NewUser("farmer-a", "Farmer", "0901234567");
        var farmerB = NewUser("farmer-b", "Farmer", "0901234568");
        var farmerNew = NewUser("farmer-new", "Farmer", "0901234569");
        var wholesale = NewUser("wholesale", "WholesaleBuyer", "0933000001");
        var wholesaleNew = NewUser("wholesale-new", "WholesaleBuyer", "0933000002");
        var officer = NewUser("officer", "VerificationOfficer");
        var moderator = NewUser("moderator", "ContentModerator");
        var finance = NewUser("finance", "FinanceStaff");
        db.Users.AddRange(customer, customer2, farmerA, farmerB, farmerNew, wholesale, wholesaleNew, officer, moderator, finance);
        db.SupportedBanks.Add(new SupportedBank { BankCode = "VCB", ShortName = "Vietcombank", FullName = "Ngân hàng TMCP Ngoại thương Việt Nam", IsActive = true, DisplayOrder = 1 });
        await db.SaveChangesAsync();
        (CustomerId, Customer2Id, FarmerAId, FarmerBId, FarmerNewId) = (customer.UserId, customer2.UserId, farmerA.UserId, farmerB.UserId, farmerNew.UserId);
        (WholesaleId, WholesaleNewId, OfficerId, ModeratorId, FinanceId) = (wholesale.UserId, wholesaleNew.UserId, officer.UserId, moderator.UserId, finance.UserId);

        // Farms: two Approved farms for the supplier directory, one new farm that has not been verified yet.
        var farmA = new FarmerProfile
        {
            UserId = farmerA.UserId, FarmName = "Nông trại Bơ Lâm Đồng", RepresentativeName = "Nguyễn Văn A",
            Address = "Thôn 3, Đà Lạt", ProvinceCode = "100000", CultivationZone = "Lâm Đồng", ProductionScale = "Lớn",
            CultivatedArea = 5m, VerificationStatus = "Approved", ApprovedAt = now.AddDays(-20), ReputationScore = 4.8m
        };
        var farmB = new FarmerProfile
        {
            UserId = farmerB.UserId, FarmName = "Trang trại Rau Sạch", RepresentativeName = "Trần Thị B",
            Address = "Hoài Đức", ProvinceCode = "100000", CultivationZone = "Hà Nội", ProductionScale = "Nhỏ",
            CultivatedArea = 1.5m, VerificationStatus = "Approved", ApprovedAt = now.AddDays(-5), ReputationScore = 4.2m
        };
        var farmNew = new FarmerProfile
        {
            UserId = farmerNew.UserId, FarmName = "Vườn Mới", RepresentativeName = "Lê Văn C",
            Address = "Xã Tân Lập", ProvinceCode = "100000", CultivationZone = "Hà Nội",
            ProductionModel = "Individual", VerificationStatus = "NotSubmitted", ReputationScore = 0m
        };
        var wholesaleProfile = new WholesaleBuyerProfile
        {
            UserId = wholesale.UserId, CompanyName = "Organic Mart", TaxCode = "0101234567",
            RepresentativeName = "Trần Văn B", BusinessType = "Supermarket", BusinessAddress = "Cầu Giấy, Hà Nội",
            VerificationStatus = "Approved", ApprovedAt = now.AddDays(-10)
        };
        var category = new Category { CategoryName = "Trái cây", IsActive = true };
        db.AddRange(farmA, farmB, farmNew, wholesaleProfile, category);
        await db.SaveChangesAsync();
        (FarmAId, FarmBId, FarmNewId, WholesaleProfileId, CategoryId) =
            (farmA.FarmerProfileId, farmB.FarmerProfileId, farmNew.FarmerProfileId, wholesaleProfile.WholesaleBuyerProfileId, category.CategoryId);

        var avocado = new Product
        {
            CategoryId = category.CategoryId, FarmerProfileId = farmA.FarmerProfileId, ProductName = "Bơ 034",
            Unit = "kg", OriginProvince = "Lâm Đồng", BaseRetailPrice = 85_000, ApprovalStatus = "Approved",
            IsPublished = true, CreatedAt = now.AddDays(-15)
        };
        var spinach = new Product
        {
            CategoryId = category.CategoryId, FarmerProfileId = farmB.FarmerProfileId, ProductName = "Cải bó xôi",
            Unit = "kg", BaseRetailPrice = 15_000, ApprovalStatus = "Approved", IsPublished = true, CreatedAt = now.AddDays(-15)
        };
        db.Products.AddRange(avocado, spinach);
        db.FarmCertificates.Add(new FarmCertificate
        {
            FarmerProfileId = farmA.FarmerProfileId, CertificateType = "VietGAP", CertificateNumber = "VG-SEED-001",
            IssuedBy = "Sở NN&PTNT Lâm Đồng", IssueDate = new DateOnly(2026, 1, 10), ExpiryDate = new DateOnly(2028, 1, 10),
            DocumentUrl = "/api/dossiers/certificates/seed.pdf", VerificationStatus = "Verified", ReviewedAt = now.AddDays(-12)
        });
        await db.SaveChangesAsync();
        AvocadoId = avocado.ProductId;

        // UC-TRC-01: one traceable batch with a cultivation log, one batch whose QR was revoked.
        var season = new CropSeason
        {
            FarmerProfileId = farmA.FarmerProfileId, ProductId = avocado.ProductId, SeasonName = "Vụ bơ 2026",
            CropVariety = "Bơ 034", CultivatedArea = 2, PlantingDate = new DateOnly(2026, 3, 1),
            EstimatedHarvestDate = new DateOnly(2026, 10, 1), EstimatedYield = 500, Status = "Harvesting", YieldUnit = "kg"
        };
        db.CropSeasons.Add(season);
        await db.SaveChangesAsync();
        db.CultivationLogs.Add(new CultivationLog
        {
            CropSeasonId = season.CropSeasonId, RecordedByUserId = farmerA.UserId, ActionDate = now.AddDays(-60),
            ActionType = "Bón phân", DetailDescription = "Bón phân hữu cơ", InputMaterial = "Phân bò ủ hoai",
            Dosage = "2 kg/gốc", PerformedBy = "Nguyễn Văn A", RecordedAt = now.AddDays(-60)
        });
        var batch = new HarvestBatch
        {
            ProductId = avocado.ProductId, CropSeasonId = season.CropSeasonId, BatchCode = "BATCH-DL-001",
            ActualHarvestDate = new DateOnly(2026, 10, 20), HarvestYield = 200, AvailableQuantity = 150,
            ExpirationDate = new DateOnly(2026, 11, 10), Status = "Available", StockEnteredAt = now.AddDays(-7)
        };
        var recalled = new HarvestBatch
        {
            ProductId = avocado.ProductId, CropSeasonId = season.CropSeasonId, BatchCode = "BATCH-DL-002",
            ActualHarvestDate = new DateOnly(2026, 10, 15), HarvestYield = 100, AvailableQuantity = 0,
            ExpirationDate = new DateOnly(2026, 11, 5), Status = "Recalled", StockEnteredAt = now.AddDays(-12)
        };
        db.HarvestBatches.AddRange(batch, recalled);
        await db.SaveChangesAsync();
        db.TraceabilityQrcodes.AddRange(
            new TraceabilityQrcode
            {
                HarvestBatchId = batch.HarvestBatchId, QrcontentString = "AGRI-QR-DL-001",
                TargetUrl = "https://agrisustain.vn/trace/BATCH-DL-001", GeneratedAt = now.AddDays(-7)
            },
            new TraceabilityQrcode
            {
                HarvestBatchId = recalled.HarvestBatchId, QrcontentString = "AGRI-QR-DL-002",
                TargetUrl = "https://agrisustain.vn/trace/BATCH-DL-002", GeneratedAt = now.AddDays(-12), IsRevoked = true
            });
        await db.SaveChangesAsync();

        // UC-RC-04 / UC-CM-02 / UC-FIN-08: retail orders of the customer (and one of another customer).
        Order RetailOrder(User buyer, string code, string status, DateTime placed, int items, DateTime? deliveredAt)
        {
            var order = new Order
            {
                BuyerUserId = buyer.UserId, OrderCode = code, OrderType = "Retail", OrderDate = placed,
                ShippingAddress = "123 Cầu Giấy, Hà Nội", OrderStatus = status, RecipientNameSnapshot = buyer.FullName,
                RecipientPhoneSnapshot = buyer.PhoneNumber ?? "0912000000", CheckoutRequestKey = Guid.NewGuid().ToString("D"),
                DeliveryMethod = "Standard", PaymentMethod = "SePay",
                TotalAmount = 85_000m * items, FinalAmount = 85_000m * items,
                CompletedAt = status == "Completed" ? deliveredAt : null
            };
            var seller = new SellerOrder
            {
                FarmerProfileId = farmA.FarmerProfileId, SellerOrderCode = code + "-1", Status = status,
                Subtotal = 85_000m * items, FinalAmount = 85_000m * items, DeliveredAt = deliveredAt,
                ReceivedAt = status == "Completed" ? deliveredAt : null
            };
            for (int i = 0; i < items; i++)
            {
                seller.OrderItems.Add(new OrderItem
                {
                    ProductId = avocado.ProductId, ProductNameSnapshot = "Bơ 034", Quantity = 1, UnitSnapshot = "kg",
                    UnitPriceSnapshot = 85_000, SubTotal = 85_000, ItemStatus = status, HarvestBatchId = batch.HarvestBatchId
                });
            }
            order.SellerOrders.Add(seller);
            order.PaymentTransactions.Add(new PaymentTransaction
            {
                TransactionCode = "PAY-" + code, GatewayProvider = "SePay", Amount = 85_000m * items, PaymentType = "VietQR",
                PaymentStatus = "Success", PaymentPurpose = "Order", RequestedAt = placed, PaidAt = placed,
                RequestKey = "pay-" + code
            });
            return order;
        }
        var delivered = RetailOrder(customer, "ORD-IT-001", "Delivered", now.AddDays(-2), 1, now.AddHours(-12));
        var completed = RetailOrder(customer, "ORD-IT-002", "Completed", now.AddDays(-6), 3, now.AddDays(-4));
        var disputed = RetailOrder(customer, "ORD-IT-003", "Delivered", now.AddDays(-3), 1, now.AddHours(-20));
        var processing = RetailOrder(customer, "ORD-IT-004", "Processing", now.AddHours(-5), 1, null);
        var other = RetailOrder(customer2, "ORD-IT-201", "Delivered", now.AddDays(-1), 1, now.AddHours(-6));
        db.Orders.AddRange(delivered, completed, disputed, processing, other);
        await db.SaveChangesAsync();
        (DeliveredOrderId, CompletedOrderId, DisputedOrderId, ProcessingOrderId, OtherOrderId) =
            (delivered.OrderId, completed.OrderId, disputed.OrderId, processing.OrderId, other.OrderId);
        db.DisputeTickets.Add(new DisputeTicket
        {
            SubmittedByUserId = customer.UserId, SellerOrderId = disputed.SellerOrders.Single().SellerOrderId,
            DisputeReason = "Damaged", Description = "Bơ bị dập", TicketStatus = DisputeTicket.StatusOpen, CreatedAt = now.AddHours(-2)
        });

        // UC-CM-02: three reviews on the completed order: clean, flagged by the vulgar-word rule, already hidden.
        var items3 = completed.SellerOrders.Single().OrderItems.OrderBy(i => i.OrderItemId).ToList();
        ProductReview Review(OrderItem item, string comment, string status, string? reason, int daysAgo) => new()
        {
            OrderItemId = item.OrderItemId, UserId = customer.UserId, RatingStars = 4, Comment = comment,
            ReviewDate = now.AddDays(-daysAgo), IsPurchasedVerified = true, ModerationStatus = status, ModerationReason = reason
        };
        var clean = Review(items3[0], "Bơ rất ngon, dẻo và béo.", "Approved", null, 3);
        var flagged = Review(items3[1], "Shop vcl, giao hàng như hạch", "Approved", null, 2);
        var hidden = Review(items3[2], "Spam quảng cáo", "Hidden", "Spam", 1);
        db.ProductReviews.AddRange(clean, flagged, hidden);

        // UC-FIN-08: refunds of the customer's paid order (no complaint, so the order buyer owns them).
        var deliveredPayment = delivered.PaymentTransactions.Single();
        var completedPayment = completed.PaymentTransactions.Single();
        var otherPayment = other.PaymentTransactions.Single();
        RefundTransaction Refund(PaymentTransaction payment, string code, decimal amount, string status) => new()
        {
            PaymentTransactionId = payment.PaymentTransactionId, ProcessedByUserId = finance.UserId, RefundCode = code,
            Amount = amount, Reason = "Hoàn tiền sản phẩm hỏng", RefundStatus = status, RequestKey = "refund-" + code,
            ProcessedAt = status == "Completed" ? now.AddDays(-1) : null
        };
        var pendingRefund = Refund(deliveredPayment, "RFD-IT-001", 85_000, "Pending");
        var completedRefund = Refund(completedPayment, "RFD-IT-002", 85_000, "Completed");
        var otherRefund = Refund(otherPayment, "RFD-IT-201", 85_000, "Pending");
        db.RefundTransactions.AddRange(pendingRefund, completedRefund, otherRefund);

        // UC-WB-08: the three membership tiers.
        B2bsubscriptionPackage Tier(string name, decimal price, int days, bool contact) => new()
        {
            PackageName = name, PackageDescription = name, DurationDays = days, Price = price,
            UnlocksFarmerDirectContact = contact, CanDownloadContractTemplate = contact, IsActive = true
        };
        var free = Tier("Cơ bản", 0, 30, false);
        var silver = Tier("Bạc", 500_000, 30, true);
        var gold = Tier("Vàng", 5_000_000, 365, true);
        db.B2bsubscriptionPackages.AddRange(free, silver, gold);

        // UC-FIN-06: an Active offtake contract (one overdue line, one future line) and a Terminated one.
        B2bcontract Contract(string number, string status) => new()
        {
            WholesaleBuyerProfileId = wholesaleProfile.WholesaleBuyerProfileId, FarmerProfileId = farmA.FarmerProfileId,
            ContractNumber = number, Status = status, DigitalSignatureStatus = "FullySigned", SignedDate = now.AddDays(-30),
            CurrentVersionNo = 1, ValidFrom = new DateOnly(2026, 10, 1), ValidUntil = new DateOnly(2027, 3, 31), CreatedAt = now.AddDays(-30)
        };
        ContractMilestone Line(B2bcontract contract, string title, DateOnly due, decimal amount) => new()
        {
            B2bcontract = contract, MilestoneTitle = title, MilestoneType = "Installment", DueDate = due, Amount = amount,
            FulfillmentStatus = "Pending", PaymentStatus = "Unpaid"
        };
        var contract = Contract("HD-IT-001", "Active");
        var version = new ContractVersion
        {
            VersionNo = 1, ProposedByUserId = wholesale.UserId, TermsAndConditions = "Điều khoản bao tiêu", VersionStatus = "Signed",
            CreatedAt = now.AddDays(-30)
        };
        var overdueLine = Line(contract, "Thanh toán đợt 1", new DateOnly(2026, 10, 24), 1_000_000);
        var futureLine = Line(contract, "Thanh toán đợt 2", new DateOnly(2026, 11, 20), 2_000_000);
        version.ContractMilestones.Add(overdueLine);
        version.ContractMilestones.Add(futureLine);
        contract.ContractVersions.Add(version);
        var terminated = Contract("HD-IT-002", "Terminated");
        var terminatedVersion = new ContractVersion
        {
            VersionNo = 1, ProposedByUserId = wholesale.UserId, TermsAndConditions = "Điều khoản", VersionStatus = "Signed",
            CreatedAt = now.AddDays(-30)
        };
        var terminatedLine = Line(terminated, "Thanh toán đợt 1", new DateOnly(2026, 10, 25), 700_000);
        terminatedVersion.ContractMilestones.Add(terminatedLine);
        terminated.ContractVersions.Add(terminatedVersion);
        db.B2bcontracts.AddRange(contract, terminated);
        await db.SaveChangesAsync();

        (CleanReviewId, FlaggedReviewId, HiddenReviewId) = (clean.ProductReviewId, flagged.ProductReviewId, hidden.ProductReviewId);
        (PendingRefundId, CompletedRefundId, OtherRefundId) =
            (pendingRefund.RefundTransactionId, completedRefund.RefundTransactionId, otherRefund.RefundTransactionId);
        (FreeTierId, SilverTierId, GoldTierId) = (free.PackageId, silver.PackageId, gold.PackageId);
        (ContractId, OverdueLineId, FutureLineId) = (contract.B2bcontractId, overdueLine.ContractMilestoneId, futureLine.ContractMilestoneId);
        (TerminatedContractId, TerminatedLineId) = (terminated.B2bcontractId, terminatedLine.ContractMilestoneId);
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

        try { Directory.Delete(_contentRoot, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Captures every e-mail the application sends through the Gmail notification gateway.</summary>
internal sealed class MailSink : INotificationGateway
{
    public ConcurrentQueue<(string Recipient, string Subject, string EventKey)> Sent { get; } = new();

    public Task SendEmailAsync(string recipient, string subject, string content, string eventKey,
        CancellationToken cancellationToken)
    {
        Sent.Enqueue((recipient, subject, eventKey));
        return Task.CompletedTask;
    }
}

/// <summary>Captures the "your bank account was changed" e-mail of UC-AUTH-03 (BR-BA-04).</summary>
internal sealed class BankChangeMailSink : IBankAccountEmailSender
{
    public ConcurrentQueue<(string Email, string? Last4)> Sent { get; } = new();

    public Task<bool> SendBankAccountChangedAsync(string email, string? bankShortName, string? last4,
        DateTime changedAtUtc, CancellationToken cancellationToken)
    {
        Sent.Enqueue((email, last4));
        return Task.FromResult(true);
    }
}
