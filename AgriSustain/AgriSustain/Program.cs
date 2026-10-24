using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
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

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Checkout.json", optional: false)
    .AddEnvironmentVariables().AddCommandLine(args);
builder.Services.AddThreeUseCases(builder.Configuration);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));

// Existing team auth services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();
builder.Services.AddScoped<IMarketplaceRepository, MarketplaceRepository>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

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
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CartPricingService>();
builder.Services.AddScoped<FulfillmentRules>();
builder.Services.AddScoped<OrderFulfillmentService>();
builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();

// DuND Application Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IOrderTrackingService, OrderTrackingService>();

// DuND Validators
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<VerifyOtpRequest>, VerifyOtpRequestValidator>();
builder.Services.AddScoped<IValidator<ResendOtpRequest>, ResendOtpRequestValidator>();
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

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActiveFeatureAccountMiddleware>();
app.MapControllers();

app.Run();
