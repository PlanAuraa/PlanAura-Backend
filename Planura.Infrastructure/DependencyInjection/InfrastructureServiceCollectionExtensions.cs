using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Application.Abstraction.Authentication;
using Planura.Core.Application.Abstraction.Notifications;
using Planura.Core.Application.Abstraction.Storage;
using Planura.Infrastructure.Authentication;
using Planura.Infrastructure.Authorization;
using Planura.Infrastructure.Notifications;
using Planura.Infrastructure.Storage;

namespace Planura.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            services.AddScoped<IAuthorizationHandler, ApprovedVendorHandler>();

            services.AddScoped<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}
