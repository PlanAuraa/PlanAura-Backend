using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.AccountAdmin;
using Planura.Core.Application.Services.AdminBooking;
using Planura.Core.Application.Services.AdminClient;
using Planura.Core.Application.Services.AdminDashboard;
using Planura.Core.Application.Services.AdminPayment;
using Planura.Core.Application.Services.AdminReport;
using Planura.Core.Application.Services.AdminVendor;
using Planura.Core.Application.Services.AiChat;
using Planura.Core.Application.Services.Auth;
using Planura.Core.Application.Services.Booking;
using Planura.Core.Application.Services.BookingHoldExpiryJob;
using Planura.Core.Application.Services.CategoryService;
using System.Reflection;

namespace Planura.Core.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());
        services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountAdminService, AccountAdminService>();
        services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
        services.AddScoped<IVendorPackageService, VendorPackageService>();
        services.AddScoped<IVendorAvailabilityService, VendorAvailabilityService>();
        services.AddScoped<IVendorVerificationService, VendorVerificationService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IEventPlanService, EventPlanService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IBookingHoldExpiryJob, BookingHoldExpiryJob>();
        services.AddScoped<IAdminPaymentService, AdminPaymentService>();
        services.AddScoped<IAdminBookingService, AdminBookingService>();
        services.AddScoped<IAdminVendorService, AdminVendorService>();
        services.AddScoped<IAdminClientService, AdminClientService>();
        services.AddScoped<IAdminReportService, AdminReportService>();
        return services;
    }
}
