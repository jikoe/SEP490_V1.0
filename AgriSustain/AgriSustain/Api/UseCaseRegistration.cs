using System.Net.Http.Headers;
using AgriSustain.Api.Security;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Services.Checkout;
using AgriSustain.Infrastructure;
using AgriSustain.Infrastructure.BackgroundWorkers;
using AgriSustain.Infrastructure.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AgriSustain.Api;

public static class UseCaseRegistration
{
    public static IServiceCollection AddThreeUseCases(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("MyCnn")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:MyCnn.");
        services.AddInfrastructure(connection);
        services.AddOptions<GoshipOptions>().Bind(configuration.GetSection("Goship"))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                && (baseUri.Scheme == Uri.UriSchemeHttps || baseUri.IsLoopback)
                && (!options.Enabled
                    || !string.IsNullOrWhiteSpace(options.AccessToken)
                    && options.EstimatedStandardDeliveryDays >= 1
                    && options.EstimatedFreshExpressDeliveryDays >= 1
                    && options.ParcelLength > 0 && options.ParcelWidth > 0
                    && options.ParcelHeight > 0
                    && options.PickupLocations.Count > 0
                    && options.PickupLocations.Values.All(pickup =>
                        !string.IsNullOrWhiteSpace(pickup.City)
                        && !string.IsNullOrWhiteSpace(pickup.District))),
                "Invalid Goship shipping configuration.")
            .ValidateOnStart();
        services.AddMemoryCache();
        services.AddHttpClient<GoshipClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GoshipOptions>>().Value;
            // A trailing slash keeps relative paths such as "rates" under /api/v2/.
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            if (!string.IsNullOrWhiteSpace(options.AccessToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.AccessToken);
            }
        });
        services.AddScoped<IAddressCatalog, GoshipAddressCatalog>();
        services.AddScoped<IShippingRateProvider, GoshipShippingRateProvider>();
        services.AddOptions<CheckoutPolicy>().Bind(configuration.GetSection("Checkout"))
            .Validate(policy => policy.QuoteMinutes is >= 1 and <= 60
                && policy.ReservationMinutes is >= 1 and <= 120
                && policy.MaximumPointDiscountPercent is not (< 0 or > 100)
                && policy.PointValueVnd is not <= 0
                && policy.Areas.All(x => x.StandardFee >= 0 && x.ExpressFee >= 0
                    && x.StandardDays >= 1 && x.ExpressDays >= 1)
                && policy.Areas.GroupBy(x => (x.ProvinceCode, x.WardCode))
                    .All(x => x.Count() == 1), "Invalid Checkout policy.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<CheckoutPolicy>>().Value);
        FluentValidation.ValidatorOptions.Global.LanguageManager.Culture =
            System.Globalization.CultureInfo.GetCultureInfo("vi");
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<IActorContext, CurrentActor>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<IKycVerificationService, KycVerificationService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<ReservationExpiryService>();
        services.AddScoped<IAuthorizationHandler, ActiveAccountHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Administrator", policy => policy.RequireAuthenticatedUser()
                .RequireRole("SystemAdmin", "Administrator", "Admin")
                .AddRequirements(new ActiveAccountRequirement()));
            options.AddPolicy("RetailCustomer", policy => policy.RequireAuthenticatedUser()
                .RequireRole("RetailCustomer").AddRequirements(new ActiveAccountRequirement()));
        });
        if (configuration.GetValue<bool>("Checkout:EnableExpiryWorker"))
        {
            services.AddHostedService<ReservationExpiryWorker>();
        }
        return services;
    }
}
