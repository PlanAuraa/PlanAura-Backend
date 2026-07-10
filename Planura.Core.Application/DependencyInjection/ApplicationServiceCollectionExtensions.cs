using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Authentication;
using Planura.Core.Application.Storage;
using Planura.Core.Application.Vendors;

namespace Planura.Core.Application.DependencyInjection
{
    public static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IVendorDocumentAccessService, VendorDocumentAccessService>();
            services.AddScoped<IVendorOnboardingService, VendorOnboardingService>();
            services.AddScoped<IVendorCategoryService, VendorCategoryService>();
            services.AddScoped<IVendorVerificationService, VendorVerificationService>();
            services.AddScoped<IAdminVerificationService, AdminVerificationService>();

            return services;
        }
    }
}
