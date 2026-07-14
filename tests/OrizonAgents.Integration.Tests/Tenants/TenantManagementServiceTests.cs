using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Integration.Tests.Authentication;

namespace OrizonAgents.Integration.Tests.Tenants;

public class TenantManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesTenantSettingsAndFirstTenantAdmin()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        OperationResult<Guid> result = await service.CreateAsync(CreateTenant("Orizon Test", "Orizon Test"));

        Assert.True(result.Succeeded, result.FirstError);
        ApplicationUser admin = (await userManager.FindByEmailAsync("admin@orizon.test"))!;
        Assert.NotNull(admin);
        Assert.Equal(result.Value, admin.TenantId);
        Assert.True(await userManager.IsInRoleAsync(admin, OrizonRoles.TenantAdmin));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidAdminPassword_DoesNotKeepPartialTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();

        OperationResult<Guid> result = await service.CreateAsync(
            CreateTenant("Orizon Test", "orizon-test", password: "fraca"));

        Assert.False(result.Succeeded);
        Assert.Empty(dbContext.Tenants);
        Assert.Empty(dbContext.TenantSettings);
    }

    [Fact]
    public async Task CreateAsync_NormalizesSlugAndRejectsDuplicateSlug()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();

        OperationResult<Guid> created = await service.CreateAsync(CreateTenant("Orizon Test", "Órizon Test"));
        OperationResult<Guid> duplicate = await service.CreateAsync(
            CreateTenant("Outra", "orizon-test", adminEmail: "outro@orizon.test"));
        TenantDetailsDto details = (await service.GetDetailsAsync(created.Value))!;

        Assert.Equal("orizon-test", details.Slug);
        Assert.False(duplicate.Succeeded);
        Assert.Contains("slug", duplicate.FirstError);
    }

    [Fact]
    public async Task ListAsync_SearchFilterAndPagination_RunInDatabaseShape()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        await service.CreateAsync(CreateTenant("Alpha", "alpha"));
        await service.CreateAsync(CreateTenant("Beta", "beta", adminEmail: "beta@orizon.test"));
        TenantDetailsDto beta = (await service.GetDetailsAsync((await service.ListAsync(new TenantListRequest("beta", null, null))).Items.Single().Id))!;
        await service.SuspendAsync(new SuspendTenantRequest(beta.Id, "Contrato em revisão", beta.ConcurrencyStamp));

        PagedResult<TenantListItemDto> result = await service.ListAsync(new TenantListRequest("beta", "Suspended", "slug", 1, 5));

        Assert.Single(result.Items);
        Assert.Equal("beta", result.Items.Single().Slug);
        Assert.Equal("Suspended", result.Items.Single().Status);
    }

    [Fact]
    public async Task UpdateSuspendAndReactivateAsync_ApplyTenantRules()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        OperationResult<Guid> created = await service.CreateAsync(CreateTenant("Orizon Test", "orizon-test"));
        TenantDetailsDto details = (await service.GetDetailsAsync(created.Value))!;

        OperationResult updated = await service.UpdateAsync(new UpdateTenantRequest(
            created.Value,
            "Orizon Novo",
            "orizon-novo",
            "pt-BR",
            "America/Sao_Paulo",
            "Operações",
            "ops@orizon.test",
            "+55 11 99999-0000",
            details.ConcurrencyStamp));
        TenantDetailsDto updatedDetails = (await service.GetDetailsAsync(created.Value))!;
        OperationResult suspended = await service.SuspendAsync(new SuspendTenantRequest(created.Value, "Contrato em revisão", updatedDetails.ConcurrencyStamp));
        TenantDetailsDto suspendedDetails = (await service.GetDetailsAsync(created.Value))!;
        OperationResult reactivated = await service.ReactivateAsync(new ReactivateTenantRequest(created.Value, suspendedDetails.ConcurrencyStamp));

        Assert.True(updated.Succeeded, updated.FirstError);
        Assert.True(suspended.Succeeded, suspended.FirstError);
        Assert.True(reactivated.Succeeded, reactivated.FirstError);
        Assert.Equal("Orizon Novo", updatedDetails.Name);
        Assert.Equal("ops@orizon.test", updatedDetails.ContactEmail);
        Assert.Equal("Active", (await service.GetDetailsAsync(created.Value))!.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithOldConcurrencyStamp_Fails()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        OperationResult<Guid> created = await service.CreateAsync(CreateTenant("Orizon Test", "orizon-test"));

        OperationResult result = await service.UpdateAsync(new UpdateTenantRequest(
            created.Value,
            "Orizon Novo",
            "orizon-novo",
            "pt-BR",
            "America/Sao_Paulo",
            null,
            null,
            null,
            "stale"));

        Assert.False(result.Succeeded);
        Assert.Contains("alterada por outro usuário", result.FirstError);
    }

    [Fact]
    public async Task UpdateOwnSettingsAsync_UpdatesOnlyRequestedTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<ITenantManagementService>();
        OperationResult<Guid> tenantA = await service.CreateAsync(CreateTenant("Tenant A", "tenant-a"));
        OperationResult<Guid> tenantB = await service.CreateAsync(CreateTenant("Tenant B", "tenant-b", adminEmail: "b@orizon.test"));
        TenantOrganizationDto organization = (await service.GetOrganizationAsync(tenantA.Value))!;

        OperationResult result = await service.UpdateOwnSettingsAsync(new UpdateOwnTenantSettingsRequest(
            tenantA.Value,
            "Tenant A Atualizado",
            "en-US",
            "UTC",
            "Contato A",
            "contato-a@orizon.test",
            null,
            organization.ConcurrencyStamp));

        Assert.True(result.Succeeded, result.FirstError);
        Assert.Equal("Tenant A Atualizado", (await service.GetOrganizationAsync(tenantA.Value))!.Name);
        Assert.Equal("Tenant B", (await service.GetOrganizationAsync(tenantB.Value))!.Name);
    }

    [Fact]
    public async Task PasswordSignInAsync_BlocksSuspendedTenantLogin()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var tenantService = provider.GetRequiredService<ITenantManagementService>();
        var accountService = provider.GetRequiredService<IAccountService>();
        OperationResult<Guid> created = await tenantService.CreateAsync(CreateTenant("Orizon Test", "orizon-test"));
        TenantDetailsDto details = (await tenantService.GetDetailsAsync(created.Value))!;
        await tenantService.SuspendAsync(new SuspendTenantRequest(created.Value, "Contrato em revisão", details.ConcurrencyStamp));

        OperationResult result = await accountService.PasswordSignInAsync(new LoginRequest(
            "admin@orizon.test",
            AuthenticationTestFixture.ValidPassword,
            false));

        Assert.False(result.Succeeded);
        Assert.Contains("suspensa", result.FirstError);
    }

    private static CreateTenantRequest CreateTenant(
        string name,
        string slug,
        string adminEmail = "admin@orizon.test",
        string password = AuthenticationTestFixture.ValidPassword)
    {
        return new CreateTenantRequest(
            name,
            slug,
            "pt-BR",
            "America/Sao_Paulo",
            "Operações",
            "ops@orizon.test",
            null,
            "Admin Tenant",
            adminEmail,
            password,
            password);
    }
}
