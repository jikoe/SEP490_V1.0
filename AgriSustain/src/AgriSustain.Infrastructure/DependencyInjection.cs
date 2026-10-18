using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Data, Repositories, UnitOfWork and adapters implementing Application.Interfaces.
        // BackgroundWorkers invoke Application.Services within an appropriate DI scope.
        return services;
    }
}
