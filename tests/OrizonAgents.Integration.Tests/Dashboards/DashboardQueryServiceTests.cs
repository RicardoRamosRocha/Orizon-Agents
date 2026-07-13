using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Application.Dashboards.Models;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Integration.Tests.Authentication;

namespace OrizonAgents.Integration.Tests.Dashboards;

public class DashboardQueryServiceTests
{
    [Fact]
    public async Task GetTenantDashboardAsync_ReturnsMetricsForCurrentTenantOnly()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        Tenant tenantA = Tenant.Create("Tenant A", "tenant-a");
        Tenant tenantB = Tenant.Create("Tenant B", "tenant-b");
        dbContext.Tenants.AddRange(tenantA, tenantB);
        await dbContext.SaveChangesAsync();
        ApplicationUser adminA = await CreateUserAsync(userManager, tenantA.Id, "Admin A", "admin-a@orizon.test", OrizonRoles.TenantAdmin);
        await CreateUserAsync(userManager, tenantA.Id, "Member A", "member-a@orizon.test", OrizonRoles.TenantMember);
        await CreateUserAsync(userManager, tenantB.Id, "Member B", "member-b@orizon.test", OrizonRoles.TenantMember);

        var result = await service.GetTenantDashboardAsync(tenantA.Id, adminA.Id);

        Assert.True(result.Succeeded, result.FirstError);
        TenantDashboardDto dashboard = result.Value!;
        Assert.Equal("Tenant A", dashboard.TenantName);
        Assert.Equal(2, dashboard.Metrics.Single(metric => metric.Label == "Usuários").Value);
        Assert.Equal(1, dashboard.Metrics.Single(metric => metric.Label == "Administradores").Value);
        Assert.DoesNotContain(dashboard.RecentUsers, user => user.Email == "member-b@orizon.test");
    }

    [Fact]
    public async Task GetTenantDashboardAsync_ChecklistReflectsRealConfiguration()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        ApplicationUser admin = await CreateUserAsync(userManager, tenant.Id, "Admin A", "admin-a@orizon.test", OrizonRoles.TenantAdmin, DateTime.UtcNow.AddDays(-4));

        TenantDashboardDto dashboard = (await service.GetTenantDashboardAsync(tenant.Id, admin.Id)).Value!;

        Assert.Contains(dashboard.SetupChecklist, item => item.Label == "Organização criada" && item.IsComplete);
        Assert.Contains(dashboard.SetupChecklist, item => item.Label == "Perfil do administrador configurado" && item.IsComplete);
        Assert.Contains(dashboard.SetupChecklist, item => item.Label == "Configurações regionais definidas" && item.IsComplete);
        Assert.Contains(dashboard.SetupChecklist, item => item.Label == "Usuário adicional cadastrado" && !item.IsComplete);
    }

    [Fact]
    public async Task GetPlatformDashboardAsync_ReturnsAggregateMetricsAndRecentRecords()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        Tenant active = Tenant.Create("Tenant Active", "tenant-active");
        Tenant suspended = Tenant.Create("Tenant Suspended", "tenant-suspended");
        suspended.ChangeStatus(TenantStatus.Suspended);
        dbContext.Tenants.AddRange(active, suspended);
        await dbContext.SaveChangesAsync();
        await CreateUserAsync(userManager, active.Id, "Active User", "active@orizon.test", OrizonRoles.TenantAdmin);
        ApplicationUser inactive = await CreateUserAsync(userManager, suspended.Id, "Inactive User", "inactive@orizon.test", OrizonRoles.TenantMember);
        inactive.IsActive = false;
        await userManager.UpdateAsync(inactive);

        PlatformDashboardDto dashboard = await service.GetPlatformDashboardAsync();

        Assert.Equal(2, dashboard.Metrics.Single(metric => metric.Label == "Tenants").Value);
        Assert.Equal(1, dashboard.Metrics.Single(metric => metric.Label == "Tenants ativos").Value);
        Assert.Equal(1, dashboard.Metrics.Single(metric => metric.Label == "Tenants suspensos/inativos").Value);
        Assert.Equal(2, dashboard.Metrics.Single(metric => metric.Label == "Usuários").Value);
        Assert.Equal(2, dashboard.RecentTenants.Count);
        Assert.Equal(2, dashboard.RecentUsers.Count);
    }

    [Fact]
    public async Task GetTenantDashboardAsync_OrdersRecentUsersByCreationDate()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        ApplicationUser admin = await CreateUserAsync(userManager, tenant.Id, "Admin A", "admin-a@orizon.test", OrizonRoles.TenantAdmin, DateTime.UtcNow.AddDays(-4));
        await CreateUserAsync(userManager, tenant.Id, "Old User", "old@orizon.test", OrizonRoles.TenantMember, DateTime.UtcNow.AddDays(-3));
        await CreateUserAsync(userManager, tenant.Id, "Newest User", "newest@orizon.test", OrizonRoles.TenantMember, DateTime.UtcNow.AddMinutes(-1));

        TenantDashboardDto dashboard = (await service.GetTenantDashboardAsync(tenant.Id, admin.Id)).Value!;

        Assert.Equal("Newest User", dashboard.RecentUsers.First().FullName);
    }

    [Fact]
    public async Task GetPlatformDashboardAsync_OrdersRecentTenantsAndUsersByCreationDate()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        Tenant oldTenant = Tenant.Create("Old Tenant", "old-tenant");
        Tenant newestTenant = Tenant.Create("Newest Tenant", "newest-tenant");
        dbContext.Tenants.AddRange(oldTenant, newestTenant);
        await dbContext.SaveChangesAsync();
        SetCreatedAt(dbContext, oldTenant, DateTime.UtcNow.AddDays(-10));
        SetCreatedAt(dbContext, newestTenant, DateTime.UtcNow.AddDays(-1));
        await dbContext.SaveChangesAsync();
        await CreateUserAsync(userManager, oldTenant.Id, "Old User", "old@orizon.test", OrizonRoles.TenantMember, DateTime.UtcNow.AddDays(-5));
        await CreateUserAsync(userManager, newestTenant.Id, "Newest User", "newest@orizon.test", OrizonRoles.TenantMember, DateTime.UtcNow.AddMinutes(-2));

        PlatformDashboardDto dashboard = await service.GetPlatformDashboardAsync();

        Assert.Equal("Newest Tenant", dashboard.RecentTenants.First().Name);
        Assert.Equal("Newest User", dashboard.RecentUsers.First().FullName);
    }

    [Fact]
    public async Task GetPlatformDashboardAsync_AllowsPlatformAdminWithoutTenantData()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IDashboardQueryService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await CreatePlatformAdminAsync(userManager);

        PlatformDashboardDto dashboard = await service.GetPlatformDashboardAsync();

        Assert.Equal(1, dashboard.Metrics.Single(metric => metric.Label == "Usuários").Value);
        Assert.Contains(dashboard.RecentUsers, user => user.TenantName is null);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        Guid tenantId,
        string fullName,
        string email,
        string role,
        DateTime? createdAtUtc = null)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            TenantId = tenantId,
            IsActive = true,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(user, AuthenticationTestFixture.ValidPassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static void SetCreatedAt(OrizonAgentsDbContext dbContext, Tenant tenant, DateTime createdAtUtc)
    {
        dbContext.Entry(tenant).Property(nameof(Tenant.CreatedAtUtc)).CurrentValue = createdAtUtc;
        dbContext.Entry(tenant).Property(nameof(Tenant.CreatedAtUtc)).IsModified = true;
    }

    private static async Task<ApplicationUser> CreatePlatformAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var user = new ApplicationUser
        {
            UserName = "platform@orizon.test",
            Email = "platform@orizon.test",
            EmailConfirmed = true,
            FullName = "Platform Admin",
            TenantId = null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(user, AuthenticationTestFixture.ValidPassword);
        Assert.True(result.Succeeded);
        await userManager.AddToRoleAsync(user, OrizonRoles.PlatformAdmin);
        return user;
    }
}
