using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register persistence and implementations of Application.Abstractions here.
        // Register background jobs that invoke Application.Features within a DI scope.
        return services;
    }
}
