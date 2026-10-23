using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Application.Validators.Orders;
using FluentValidation;

namespace AgriSustain.Api;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers only the three assigned application services and approved request validators
    /// with scoped lifetimes, leaving infrastructure and external UC implementations outside
    /// Application.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountStatusService, AccountStatusService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IValidator<UserQuery>, UserQueryValidator>();
        services.AddScoped<IValidator<AccountActionRequest>, AccountActionValidator>();
        services.AddScoped<IValidator<CategoryQuery>, CategoryQueryValidator>();
        services.AddScoped<IValidator<SaveCategoryRequest>, SaveCategoryValidator>();
        services.AddScoped<IValidator<CheckoutInput>, CheckoutInputValidator>();
        services.AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderValidator>();
        return services;
    }
}
