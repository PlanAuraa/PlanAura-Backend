using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Services;
using System.Reflection;

namespace Planura.Core.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountAdminService, AccountAdminService>();
        services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
        services.AddScoped<IVendorPackageService, VendorPackageService>();
        services.AddScoped<IVendorAvailabilityService, VendorAvailabilityService>();
        services.AddScoped<IVendorVerificationService, VendorVerificationService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
