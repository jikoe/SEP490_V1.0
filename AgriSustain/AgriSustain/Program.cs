using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Api.Auth;
using AgriSustain.Application.Validators;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using TeamUnitOfWork = AgriSustain.Application.Interfaces.UnitOfWork.IUnitOfWork;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Application.Validators.Orders;
using AgriSustain.Application.Validators.Profiles;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AgriSustain.Api;
using AgriSustain.Infrastructure.Auth;

AgriSustain.Api.DotEnv.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Checkout.json", optional: false)
    .AddEnvironmentVariables().AddCommandLine(args);
builder.Services.AddThreeUseCases(builder.Configuration);
builder.Services.AddSingleton<RevokedTokenStore>();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));

// Existing team auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IProductModerationRepository, ProductModerationRepository>();
builder.Services.AddScoped<IProductModerationService, ProductModerationService>();
builder.Services.AddScoped<IComplaintRepository, ComplaintRepository>();
// UC-FAR-10: controller -> application service -> owned complaint repository.
builder.Services.AddScoped<IFarmerComplaintRepository, FarmerComplaintRepository>();
builder.Services.AddScoped<IFarmerComplaintFiles, FarmerComplaintFileStorage>();
builder.Services.AddScoped<AgriSustain.Application.Services.Complaints.FarmerComplaintService>();
builder.Services.AddScoped<AgriSustain.Api.Controllers.FarmerComplaintInputFilter>();
if (builder.Configuration.GetValue("FarmerComplaints:EnableNotificationWorker", true))
{
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.FarmerComplaintNotificationWorker>();
}
builder.Services.AddScoped<AgriSustain.Application.Services.Complaints.ComplaintResolutionService>();
builder.Services.AddScoped<IComplaintResolutionService>(services => services
    .GetRequiredService<AgriSustain.Application.Services.Complaints.ComplaintResolutionService>());
builder.Services.AddScoped<IComplaintEvidenceTimeoutProcessor>(services => services
    .GetRequiredService<AgriSustain.Application.Services.Complaints.ComplaintResolutionService>());
// UC-CS-01: auto-resolve complaints after the 48-hour information request expires (on by default).
if (builder.Configuration.GetValue("Complaints:EnableEvidenceTimeoutWorker", true))
{
    builder.Services.AddHostedService<
        AgriSustain.Infrastructure.BackgroundWorkers.ComplaintEvidenceTimeoutWorker>();
}
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<IRefundProcessingService,
    AgriSustain.Application.Services.Refunds.RefundProcessingService>();
builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
builder.Services.AddScoped<IVoucherManagementService,
    AgriSustain.Application.Services.Promotions.VoucherManagementService>();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ValidIssuer),
        "JWT:ValidIssuer must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ValidAudience),
        "JWT:ValidAudience must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Secret)
        && Encoding.UTF8.GetByteCount(options.Secret) >= 32,
        "JWT:Secret must contain at least 32 UTF-8 bytes.")
    .ValidateOnStart();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();
builder.Services.AddScoped<IMarketplaceRepository, MarketplaceRepository>();
builder.Services.AddScoped<IOrderFeedbackService, OrderFeedbackService>();
builder.Services.AddScoped<IOrderFeedbackRepository, OrderFeedbackRepository>();
builder.Services.AddScoped<IOrderFeedbackStorage, OrderFeedbackStorage>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<IFinanceService, AgriSustain.Application.Services.Finance.FinanceService>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
        policy.SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// Standard Repositories and UnitOfWork
builder.Services.AddSingleton<SqlDemoDependencies>();
builder.Services.AddScoped<ICurrentUser, CurrentActor>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
// Both feature sets share the same scoped transaction/context instance.
builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddScoped<TeamUnitOfWork>(services => services.GetRequiredService<UnitOfWork>());
builder.Services.AddScoped<AgriSustain.Application.Interfaces.IUnitOfWork>(services => services.GetRequiredService<UnitOfWork>());

builder.Services.Configure<QuantityOptions>(builder.Configuration.GetSection("Quantity"));
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<IFarmerProductRepository>(services => services.GetRequiredService<IProductRepository>());
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IRetailCartRepository>(services => services.GetRequiredService<ICartRepository>());
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IFarmerOrderRepository>(services => services.GetRequiredService<IOrderRepository>());
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<AgriSustain.Infrastructure.Payments.BankTransferReconciler>();
builder.Services.AddScoped<IFarmerCropSeasonRepository, FarmerCropSeasonRepository>();
builder.Services.AddScoped<IFarmerFarmingLogRepository, FarmerFarmingLogRepository>();
builder.Services.AddScoped<IFarmerHarvestBatchRepository, FarmerHarvestBatchRepository>();
builder.Services.AddScoped<IFarmerEvidenceStorage, FarmerEvidenceStorage>();
builder.Services.AddScoped<FarmerCropSeasonService>();
builder.Services.AddScoped<FarmerFarmingLogService>();
builder.Services.AddScoped<FarmerHarvestBatchService>();
builder.Services.AddScoped<FarmerCropSeasonRules>();
builder.Services.AddScoped<FarmerFarmingLogRules>();
builder.Services.AddScoped<FarmerHarvestBatchRules>();
builder.Services.AddScoped<FarmerTraceabilityLabelService>();
builder.Services.AddScoped<AgriSustain.Api.Controllers.FarmInputFilter>();
builder.Services.AddScoped<FarmerProductService>();
builder.Services.AddScoped<ProductListingRules>();
builder.Services.AddScoped<ProductNotificationService>();
builder.Services.AddScoped<IProductNotificationRepository, ProductNotificationRepository>();
// Email links and Gmail transport are configured separately.
builder.Services.Configure<AgriSustain.Infrastructure.Email.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<INotificationGateway, AgriSustain.Infrastructure.Email.NotificationGateway>();
if (builder.Configuration.GetValue("ProductNotifications:EnableWorker", true))
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.ProductNotificationWorker>();
builder.Services.AddScoped<RetailCartService>();
builder.Services.AddScoped<CartPricingService>();
builder.Services.AddScoped<FulfillmentRules>();
builder.Services.AddScoped<OrderFulfillmentService>();
builder.Services.AddScoped<OrderRefundService>();
builder.Services.AddScoped<OrderNotificationService>();
builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.OrderNotificationWorker>();
builder.Services.AddScoped<FarmerOrderService>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();

// DuND Application Services
// API-02 Gmail Notification Service: real Gmail SMTP by default; Gmail__Provider=Log keeps the
// local adapter that writes the OTP to the server log.
builder.Services.AddOptions<AgriSustain.Infrastructure.Email.GmailOptions>()
    .Bind(builder.Configuration.GetSection(AgriSustain.Infrastructure.Email.GmailOptions.SectionName))
    .Validate(options => !options.UsesGmail
            || (!string.IsNullOrWhiteSpace(options.SenderAddress)
                && !string.IsNullOrWhiteSpace(options.AppPassword)),
        "Gmail__SenderAddress and Gmail__AppPassword must be set in .env (or set Gmail__Provider=Log).")
    .ValidateOnStart();
if (builder.Configuration.GetValue("Gmail:Provider", "Gmail")!
    .Equals("Log", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailService, AgriSustain.Infrastructure.Email.LoggingEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, AgriSustain.Infrastructure.Email.GmailEmailService>();
}
builder.Services.AddSingleton<IOtpGenerator, RandomOtpGenerator>();
builder.Services.AddSingleton<IOtpChallengeStore,
    AgriSustain.Infrastructure.Data.InMemoryStore.InMemoryOtpChallengeStore>();
builder.Services.AddScoped<IEmailOtpVerifier, EmailOtpVerifier>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
// UC-AUTH-03 Manage Bank Account (BR-BA-01..06). The VietQR lookup is faked in development.
builder.Services.AddScoped<AgriSustain.Application.Interfaces.Repositories.IBankAccountRepository,
    AgriSustain.Infrastructure.Repositories.BankAccountRepository>();
builder.Services.AddScoped<IBankAccountLookup, AgriSustain.Infrastructure.Banking.FakeBankAccountLookup>();
builder.Services.AddSingleton<IBankAccountEmailSender, AgriSustain.Infrastructure.Email.BankAccountEmailSender>();
builder.Services.AddScoped<IBankAccountService, BankAccountService>();
// DuND ITE2: UC-AUTH-02, UC-FAR-07, UC-CM-02, UC-TRC-01, UC-FIN-08
builder.Services.AddSingleton<AgriSustain.Infrastructure.FileStorage.DossierStorage>();
builder.Services.AddSingleton<IDossierStorage>(services =>
    services.GetRequiredService<AgriSustain.Infrastructure.FileStorage.DossierStorage>());
builder.Services.AddScoped<AgriSustain.Application.Services.BusinessProfiles.IBusinessProfileRepository, AgriSustain.Infrastructure.Repositories.BusinessProfileRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.BusinessProfiles.IBusinessProfileService, AgriSustain.Application.Services.BusinessProfiles.BusinessProfileService>();
builder.Services.AddScoped<AgriSustain.Application.Services.Certificates.IFarmCertificateRepository,
    AgriSustain.Infrastructure.Repositories.FarmCertificateRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.Certificates.IFarmCertificateService,
    AgriSustain.Application.Services.Certificates.FarmCertificateService>();
builder.Services.AddScoped<AgriSustain.Application.Services.Moderation.IReviewModerationRepository,
    AgriSustain.Infrastructure.Repositories.ReviewModerationRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.Moderation.IReviewModerationService,
    AgriSustain.Application.Services.Moderation.ReviewModerationService>();
builder.Services.AddScoped<AgriSustain.Application.Services.Traceability.ITraceabilityRepository,
    AgriSustain.Infrastructure.Repositories.TraceabilityRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.Traceability.ITraceabilityService,
    AgriSustain.Application.Services.Traceability.TraceabilityService>();
builder.Services.AddScoped<AgriSustain.Application.Services.Refunds.IRefundAccountRepository,
    AgriSustain.Infrastructure.Repositories.RefundAccountRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.Refunds.IRefundAccountService,
    AgriSustain.Application.Services.Refunds.RefundAccountService>();
// DuND ITE3: UC-WB-01 Search Suppliers
builder.Services.AddScoped<AgriSustain.Application.Services.B2B.ISupplierDirectoryRepository,
    AgriSustain.Infrastructure.Repositories.SupplierDirectoryRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.B2B.ISupplierDirectoryService,
    AgriSustain.Application.Services.B2B.SupplierDirectoryService>();
// DuND ITE3: UC-WB-08 Manage Buyer Subscription
builder.Services.AddScoped<AgriSustain.Application.Services.B2B.IBuyerSubscriptionRepository,
    AgriSustain.Infrastructure.Repositories.BuyerSubscriptionRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>();
builder.Services.AddScoped<AgriSustain.Application.Services.B2B.IBuyerSubscriptionService>(services =>
    services.GetRequiredService<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>());
builder.Services.AddSingleton<IPaymentQrProvider, AgriSustain.Infrastructure.Payments.SePayQrProvider>();
// DuND ITE3: UC-FIN-06 Track B2B Contract Payments
builder.Services.AddScoped<AgriSustain.Application.Services.Finance.IContractPaymentRepository,
    AgriSustain.Infrastructure.Repositories.ContractPaymentRepository>();
builder.Services.AddScoped<AgriSustain.Application.Services.Finance.ContractPaymentService>();
builder.Services.AddScoped<AgriSustain.Application.Services.Finance.IContractPaymentService>(services =>
    services.GetRequiredService<AgriSustain.Application.Services.Finance.ContractPaymentService>());
// SePay credits carrying "SUB..." (subscription) or "CTP..." (contract line) references.
builder.Services.AddScoped<IBankTransferHandler>(services =>
    services.GetRequiredService<AgriSustain.Application.Services.B2B.BuyerSubscriptionService>());
builder.Services.AddScoped<IBankTransferHandler>(services =>
    services.GetRequiredService<AgriSustain.Application.Services.Finance.ContractPaymentService>());
if (builder.Configuration.GetValue("B2B:EnableReminderWorkers", true))
{
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.SubscriptionReminderWorker>();
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.ContractPaymentOverdueWorker>();
}
builder.Services.AddScoped<IOrderTrackingService, OrderTrackingService>();
builder.Services.AddScoped<ISupplierCapacityRepository, SupplierCapacityRepository>();
builder.Services.AddScoped<ISupplierCapacityService, AgriSustain.Application.Services.B2B.SupplierCapacityService>();
builder.Services.AddScoped<IWholesaleOrderRepository, WholesaleOrderRepository>();
builder.Services.AddScoped<IWholesaleOrderService, AgriSustain.Application.Services.B2B.WholesaleOrderService>();

// DuND Validators
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<VerifyOtpRequest>, VerifyOtpRequestValidator>();
builder.Services.AddScoped<IValidator<ResendOtpRequest>, ResendOtpRequestValidator>();
builder.Services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsumerProfileRequest>, UpdateConsumerProfileValidator>();
builder.Services.AddScoped<IValidator<UpdateFarmerProfileRequest>, UpdateFarmerProfileValidator>();
builder.Services.AddScoped<IValidator<OrderQuery>, OrderQueryValidator>();

builder.Services.AddMemoryCache();
builder.Services.AddHostedService<RevokedTokenCleanupHostedService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]!))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var services = context.HttpContext.RequestServices;

            var tokenId = context.SecurityToken.Id;
            if (!string.IsNullOrEmpty(tokenId)
                && await services.GetRequiredService<RevokedTokenStore>().IsRevokedAsync(tokenId, context.HttpContext.RequestAborted))
            {
                context.Fail("This session has been terminated.");
                return;
            }

            var principal = context.Principal;
            var userIdStr = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal?.FindFirstValue(ClaimTypes.Role);

            if (!long.TryParse(userIdStr, out var userId))
            {
                context.Fail("Invalid user token identity.");
                return;
            }

            var cache = services.GetRequiredService<IMemoryCache>();
            var cacheKey = $"user-auth-status:{userId}:{role}";

            bool isUserValid = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
                var db = services.GetRequiredService<AgriSustainContext>();
                return await db.Users.AsNoTracking().AnyAsync(u =>
                    u.UserId == userId &&
                    u.Status == "Active" &&
                    u.IsEmailVerified &&
                    u.Role == role,
                    context.HttpContext.RequestAborted);
            });

            if (!isUserValid)
            {
                context.Fail("Tài khoản không còn đủ điều kiện truy cập hoặc đã bị tạm ngưng.");
            }
        }
    };
});
builder.Services.AddAuthorization();

// UC-FAR-08 / UC-FAR-09: farmer contract coordination and read-only financial projections.
builder.Services.AddScoped<IFarmerOfftakeContractRepository, FarmerOfftakeContractRepository>();
builder.Services.AddScoped<FarmerOfftakeContractService>();
builder.Services.AddScoped<OfftakeContractRules>();
builder.Services.AddScoped<FarmerRevenueRepository>();
builder.Services.AddScoped<IFarmerRevenueRepository>(sp => sp.GetRequiredService<FarmerRevenueRepository>());
builder.Services.AddScoped<FarmerRevenueService>();
builder.Services.AddScoped<IFarmerPaymentRequestRepository>(sp => sp.GetRequiredService<FarmerRevenueRepository>());
builder.Services.AddScoped<FarmerPaymentRequestService>();
builder.Services.AddScoped<PayoutNotificationService>();
builder.Services.AddScoped<RevenueAggregationRules>();
builder.Services.AddScoped<AgriSustain.Api.Controllers.FarmerBusinessInputFilter>();
if (builder.Configuration.GetValue("OfftakeContracts:EnableNotificationWorker", true))
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.OfftakeContractNotificationWorker>();

// UC-FIN-04: manual bank QR with verified SePay source-debit reconciliation.
builder.Services.AddScoped<IFinanceFarmerPayoutRepository, FinanceFarmerPayoutRepository>();
builder.Services.AddScoped<FinanceFarmerPayoutService>();
builder.Services.AddScoped<FarmerPayoutRules>();
builder.Services.AddScoped<AgriSustain.Api.Controllers.FinancePayoutInputFilter>();
builder.Services.AddHttpClient<IPayoutGateway, AgriSustain.Infrastructure.Payments.SePayPayoutGateway>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
if (builder.Configuration.GetValue("Payouts:EnableNotificationWorker", true))
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.FarmerPayoutNotificationWorker>();

if (builder.Configuration.GetValue("Payouts:EnableReconciliationWorker", true))
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.FarmerPayoutReconciliationWorker>();

var app = builder.Build();

app.UseCors("FrontendDevelopment");
app.UseMiddleware<FeatureExceptionMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
var publicRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(publicRoot);
var sourceImgDir = Path.Combine(publicRoot, "img");
var targetMediaImgDir = Path.Combine(publicRoot, "media", "img");
if (Directory.Exists(sourceImgDir))
{
    Directory.CreateDirectory(targetMediaImgDir);
    foreach (var filePath in Directory.GetFiles(sourceImgDir))
    {
        var destFilePath = Path.Combine(targetMediaImgDir, Path.GetFileName(filePath));
        if (!File.Exists(destFilePath))
        {
            File.Copy(filePath, destFilePath);
        }
    }
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(publicRoot)
});
app.MapGet("/health", () => new { persistence = "SqlServer" }).RequireCors("FrontendDevelopment");
app.MapGet("/health/database", async (AgriSustainContext database, CancellationToken cancellationToken) =>
{
    bool connected = await database.Database.CanConnectAsync(cancellationToken);
    return connected ? Results.Ok(new { connected }) : Results.Json(new { connected }, statusCode: 503);
}).RequireCors("FrontendDevelopment");
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/users")
    || context.Request.Path.StartsWithSegments("/api/categories")
    || context.Request.Path.StartsWithSegments("/api/checkout"), branch =>
        branch.UseMiddleware<ExceptionMiddleware>());

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
