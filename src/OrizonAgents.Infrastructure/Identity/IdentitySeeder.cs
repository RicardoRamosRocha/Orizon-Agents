using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (string role in OrizonRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        string? email = configuration["Seed:SuperAdmin:Email"];
        string? password = configuration["Seed:SuperAdmin:Password"];
        string? fullName = configuration["Seed:SuperAdmin:FullName"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fullName))
        {
            return;
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName.Trim(),
                TenantId = null,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            IdentityResult created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogWarning("Não foi possível criar PlatformAdmin configurado: {Errors}", string.Join("; ", IdentityErrorTranslator.Translate(created.Errors)));
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(user, OrizonRoles.PlatformAdmin))
        {
            await userManager.AddToRoleAsync(user, OrizonRoles.PlatformAdmin);
        }
    }
}
