using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Data.Geography;
using AgriSustain.Infrastructure.Repositories.Demo;
using AgriSustain.Infrastructure.Repositories.EfCore;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Requires Development plus explicit Demo opt-in, validates provisional commerce options,
    /// and explicitly selects SQL or isolated memory persistence, mock policies and real geography
    /// catalog. It never silently selects mocks in Production.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        // DEMO-GUARD: SQL persists business data, but auth/policy dependencies remain local
        // simulations. Both persistence modes require opt-in Development; no fallback.
        if (!isDevelopment || !configuration.GetValue<bool>("Demo:Enabled"))
        {
            throw new InvalidOperationException(
                "Three-UC demo requires Development + Demo:Enabled. "
                + "Production adapters are pending.");
        }

        services.AddOptions<DemoCommerceOptions>().Bind(configuration.GetSection("Demo"))
            .Validate(options => options.Enabled && options.ServiceableProvinceCodes.Length > 0
                && options.StandardFee >= 0 && options.ExpressFee >= 0
                && options.AdditionalSellerFee >= 0 && options.PointValue > 0
                && options.MaxPointsFraction is > 0 and <= 1
                && options.QuoteMinutes > 0 && options.ReservationMinutes > 0,
                "Invalid demo commerce assumptions.").ValidateOnStart();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        string mode = configuration["Persistence:Mode"] ?? "InMemory";
        if (mode == "SqlServer")
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("AgriSustain")))
            {
                throw new InvalidOperationException("SqlServer requires ConnectionStrings:AgriSustain.");
            }
            services.AddSingleton<SqlDemoDependencies>();
            services.AddScoped<IThreeUcUnitOfWork, EfThreeUcUnitOfWork>();
            services.AddScoped<IDemoIdentityReader, EfDemoIdentityReader>();
        }
        else if (mode == "InMemory")
        {
            services.AddSingleton<DemoUnitOfWork>();
            services.AddSingleton<IThreeUcUnitOfWork>(provider =>
                provider.GetRequiredService<DemoUnitOfWork>());
            services.AddSingleton<IDemoIdentityReader>(provider =>
                provider.GetRequiredService<DemoUnitOfWork>());
        }
        else
        {
            throw new InvalidOperationException("Unknown Persistence:Mode; no implicit fallback.");
        }
        services.AddSingleton<IAddressCatalog, VietnamAddressCatalog>();
        services.AddSingleton<ICheckoutPolicy, DemoCheckoutPolicy>();
        return services;
    }
}
