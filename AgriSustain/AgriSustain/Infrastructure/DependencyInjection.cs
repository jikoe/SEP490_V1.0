using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AgriSustainContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<IKycVerificationRepository, KycVerificationRepository>();
        services.AddScoped<IUseCaseUnitOfWork, SqlUnitOfWork>();
        services.AddSingleton<IPersistenceErrorClassifier, PersistenceErrorClassifier>();
        return services;
    }
}
