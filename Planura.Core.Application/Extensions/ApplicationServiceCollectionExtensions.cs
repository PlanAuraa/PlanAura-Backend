using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Services;
using System.Reflection;

namespace Planura.Core.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
        services.AddScoped<IVendorPackageService, VendorPackageService>();
        services.AddScoped<IVendorAvailabilityService, VendorAvailabilityService>();
        return services;
    }
}
