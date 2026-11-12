using AgentMemoryService.Users.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace AgentMemoryService.Api;

public static class RoleSeeder
{
    // Emails that should receive the Admin role on startup.
    // Add new human admins here; agent service accounts are excluded.
    private static readonly string[] AdminEmails =
    [
        "tallen@crawford-consulting.com",
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Ensure the Admin role exists
        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = adminRole,
                NormalizedName = adminRole.ToUpperInvariant()
            });

            if (result.Succeeded)
                logger.LogInformation("[RoleSeeder] Created role '{Role}'", adminRole);
            else
                logger.LogWarning("[RoleSeeder] Failed to create role '{Role}': {Errors}",
                    adminRole, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Assign Admin role to each listed email
        foreach (var email in AdminEmails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogWarning("[RoleSeeder] User '{Email}' not found — skipping role assignment", email);
                continue;
            }

            if (!await userManager.IsInRoleAsync(user, adminRole))
            {
                var result = await userManager.AddToRoleAsync(user, adminRole);
                if (result.Succeeded)
                    logger.LogInformation("[RoleSeeder] Assigned '{Role}' to '{Email}'", adminRole, email);
                else
                    logger.LogWarning("[RoleSeeder] Failed to assign '{Role}' to '{Email}': {Errors}",
                        adminRole, email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
