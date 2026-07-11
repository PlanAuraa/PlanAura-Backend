using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Planura.Core.Application.Common;
using Planura.Core.Application.Services;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;
using Planura.Infrastructure.Authorization;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Infrastructure.AttachementService;
using Planura.Infrastructure.Options;
using Planura.Infrastructure.Services;

namespace Planura.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddHttpContextAccessor();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthorizationHandler, ApprovedVendorHandler>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Enforce account suspension on every request: a suspended user (IsActive == false)
            // is rejected even when carrying an otherwise-valid JWT.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!long.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("Invalid user identifier.");
                        return;
                    }

                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    var user = await userManager.FindByIdAsync(userId.ToString());
                    if (user is null || !user.IsActive)
                    {
                        context.Fail("User account is inactive.");
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.ClientOnly, policy => policy.RequireRole(Roles.Client));
            options.AddPolicy(AuthorizationPolicies.VendorOnly, policy => policy.RequireRole(Roles.Vendor));
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ApprovedVendor, policy =>
            {
                policy.RequireRole(Roles.Vendor);
                policy.Requirements.Add(new ApprovedVendorRequirement());
            });
        });

        return services;
    }
}
