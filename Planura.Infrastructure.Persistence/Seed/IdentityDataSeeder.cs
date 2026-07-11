using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planura.Core.Domain.Constants;
using Planura.Core.Domain.Entities;

namespace Planura.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the three application roles (admin, vendor, client) and, when configured,
/// an initial admin account. Idempotent — safe to run on every startup.
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<long>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<long>(role));
            }
        }

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var adminSection = configuration.GetSection("Seed:Admin");
        var email = adminSection["Email"];
        var password = adminSection["Password"];
        var fullName = adminSection["FullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(admin, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed admin account '{email}': {DescribeErrors(createResult)}");
        }

        var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign the '{Roles.Admin}' role to seeded admin '{email}': {DescribeErrors(roleResult)}");
        }
    }

    private static string DescribeErrors(IdentityResult result)
        => string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
