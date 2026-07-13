using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Integration.Tests.Authentication;

public class TenantUserServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsOnlyUsersFromRequestedTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantUserService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenantA = Tenant.Create("Tenant A", "tenant-a");
        Tenant tenantB = Tenant.Create("Tenant B", "tenant-b");
        dbContext.Tenants.AddRange(tenantA, tenantB);
        await dbContext.SaveChangesAsync();

        await service.CreateAsync(CreateUser(tenantA.Id, "Ana Tenant", "ana@orizon.test"));
        await service.CreateAsync(CreateUser(tenantB.Id, "Bruno Tenant", "bruno@orizon.test"));

        IReadOnlyCollection<UserAccountDto> result = await service.SearchAsync(tenantA.Id, null);

        Assert.Single(result);
        Assert.Equal("ana@orizon.test", result.Single().Email);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotDeactivateLastActiveTenantAdmin()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantUserService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> created = await service.CreateAsync(
            CreateUser(tenant.Id, "Admin Tenant", "admin-tenant@orizon.test", OrizonRoles.TenantAdmin));

        OperationResult result = await service.UpdateAsync(
            new UpdateTenantUserRequest(
                tenant.Id,
                created.Value,
                "Admin Tenant",
                OrizonRoles.TenantMember,
                true,
                Guid.NewGuid()));

        Assert.False(result.Succeeded);
        Assert.Contains("TenantAdmin ativo", result.FirstError);
    }

    [Fact]
    public async Task UpdateAsync_PreventsSelfDeactivation()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantUserService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> created = await service.CreateAsync(CreateUser(tenant.Id, "Ana Tenant", "ana@orizon.test"));

        OperationResult result = await service.UpdateAsync(
            new UpdateTenantUserRequest(
                tenant.Id,
                created.Value,
                "Ana Tenant",
                OrizonRoles.TenantMember,
                false,
                created.Value));

        Assert.False(result.Succeeded);
        Assert.Contains("própria conta", result.FirstError);
    }

    private static CreateTenantUserRequest CreateUser(
        Guid tenantId,
        string fullName,
        string email,
        string role = OrizonRoles.TenantMember)
    {
        return new CreateTenantUserRequest(
            tenantId,
            fullName,
            email,
            AuthenticationTestFixture.ValidPassword,
            AuthenticationTestFixture.ValidPassword,
            role);
    }
}
