using System.Text;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Auth;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgriSustain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MyCnn");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:MyCnn must be configured.");
        }

        services.AddDbContext<AgriSustainContext>(
            options => options.UseSqlServer(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductModerationRepository, ProductModerationRepository>();
        services.AddScoped<IProductModerationService, ProductModerationService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ValidIssuer),
                "JWT:ValidIssuer must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ValidAudience),
                "JWT:ValidAudience must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Secret)
                    && Encoding.UTF8.GetByteCount(options.Secret) >= 32,
                "JWT:Secret must contain at least 32 UTF-8 bytes.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
