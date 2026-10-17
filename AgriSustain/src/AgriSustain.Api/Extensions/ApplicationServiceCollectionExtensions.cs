namespace AgriSustain.Api.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register services and validators from Application.Features here.
        // Keep startup wiring here so Application stays free of hosting dependencies.
        return services;
    }
}
