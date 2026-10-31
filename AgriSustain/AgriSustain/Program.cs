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
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddScoped<IProductModerationRepository, ProductModerationRepository>();
builder.Services.AddScoped<IProductModerationService, ProductModerationService>();
builder.Services.AddScoped<IComplaintRepository, ComplaintRepository>();
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
builder.Services.AddScoped<AgriSustain.Application.Services.Finance.FinanceSampleService>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy =>
        policy.WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
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
builder.Services.AddScoped<FarmerProductService>();
builder.Services.AddScoped<ProductListingRules>();
builder.Services.AddScoped<ProductNotificationService>();
builder.Services.AddScoped<IProductNotificationRepository, ProductNotificationRepository>();
// Review links only; NotificationGateway uses the shared GmailOptions registered below.
builder.Services.Configure<AgriSustain.Infrastructure.Email.EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<INotificationGateway, AgriSustain.Infrastructure.Email.NotificationGateway>();
if (builder.Configuration.GetValue("ProductNotifications:EnableWorker", true))
    builder.Services.AddHostedService<AgriSustain.Infrastructure.BackgroundWorkers.ProductNotificationWorker>();
builder.Services.AddScoped<RetailCartService>();
builder.Services.AddScoped<CartPricingService>();
builder.Services.AddScoped<FulfillmentRules>();
builder.Services.AddScoped<OrderFulfillmentService>();
builder.Services.AddScoped<AgriSustain.Infrastructure.Payments.PaymentGatewayProxy>();
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
builder.Services.AddScoped<IOrderTrackingService, OrderTrackingService>();

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
        OnTokenValidated = context =>
        {
            var tokenId = context.SecurityToken.Id;
            if (!string.IsNullOrEmpty(tokenId)
                && context.HttpContext.RequestServices
                    .GetRequiredService<RevokedTokenStore>()
                    .IsRevoked(tokenId))
            {
                context.Fail("This session has been terminated.");
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<FeatureExceptionMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
var publicRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(publicRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(publicRoot)
});
app.MapGet("/health", () => new { persistence = "SqlServer" });
app.MapGet("/health/database", async (AgriSustainContext database, CancellationToken cancellationToken) =>
{
    bool connected = await database.Database.CanConnectAsync(cancellationToken);
    return connected ? Results.Ok(new { connected }) : Results.Json(new { connected }, statusCode: 503);
});
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/users")
    || context.Request.Path.StartsWithSegments("/api/categories")
    || context.Request.Path.StartsWithSegments("/api/checkout"), branch =>
        branch.UseMiddleware<ExceptionMiddleware>());

app.UseCors("FrontendDevelopment");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActiveFeatureAccountMiddleware>();
app.MapControllers();

app.Run();
