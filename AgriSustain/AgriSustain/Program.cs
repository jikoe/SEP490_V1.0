using AgriSustain.Domain.Entities;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

using AgriSustain.Api.Middlewares;
using AgriSustain.Api.Security;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.FileStorage;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using AgriSustain.Application.Validators;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AgriSustainContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyCnn")));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<QuantityOptions>(builder.Configuration.GetSection("Quantity"));
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CartPricingService>();
builder.Services.AddScoped<FulfillmentRules>();
builder.Services.AddScoped<OrderFulfillmentService>();
ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("vi");
builder.Services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
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
app.MapGet("/health/database", async (AgriSustainContext context,
    CancellationToken cancellationToken) =>
{
    bool connected = await context.Database.CanConnectAsync(cancellationToken);
    return connected ? Results.Ok(new { connected })
        : Results.Json(new { connected }, statusCode: 503);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ActiveFeatureAccountMiddleware>();
app.MapControllers();

app.Run();




