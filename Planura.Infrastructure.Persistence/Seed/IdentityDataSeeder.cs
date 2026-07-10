using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planura.Core.Domain.Entities.Identity;
using Planura.Shared.Constants;

namespace Planura.Infrastructure.Persistence.Seed
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");

            await SeedRolesAsync(roleManager);
            await SeedBootstrapAdminAsync(userManager, configuration, logger);
        }

        private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
        {
            string[] roles = { Roles.Customer, Roles.Vendor, Roles.Admin };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole { Name = role });
                }
            }
        }

        private static async Task SeedBootstrapAdminAsync(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            var email = configuration["AdminSeed:Email"];
            var password = configuration["AdminSeed:Password"];
            var fullName = configuration["AdminSeed:FullName"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("AdminSeed:Email/Password are not configured; skipping bootstrap admin seeding.");
                return;
            }

            var admin = await userManager.FindByEmailAsync(email);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? "Administrator" : fullName
                };

                var createResult = await userManager.CreateAsync(admin, password);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to seed bootstrap admin account: {Errors}", errors);
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            {
                await userManager.AddToRoleAsync(admin, Roles.Admin);
            }
        }
    }
}
