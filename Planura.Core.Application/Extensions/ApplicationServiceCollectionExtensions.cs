using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using System.Reflection;

namespace Planura.Core.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());
        services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountAdminService, AccountAdminService>();
        services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
        services.AddScoped<IVendorPackageService, VendorPackageService>();
        services.AddScoped<IVendorAvailabilityService, VendorAvailabilityService>();
        services.AddScoped<IVendorVerificationService, VendorVerificationService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IEventPlanService, EventPlanService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IBookingHoldExpiryJob, BookingHoldExpiryJob>();
        return services;
    }
}
