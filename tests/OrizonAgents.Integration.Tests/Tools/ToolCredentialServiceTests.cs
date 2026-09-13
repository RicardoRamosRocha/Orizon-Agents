using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Credentials;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolCredentialServiceTests
{
    [Fact]
    public async Task CreateAsync_ProtectsSecretBeforePersisting()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantId = SetTenant(provider);
        var service = CreateService(provider);

        var result = await service.CreateAsync(new CreateToolCredentialRequest(
            tenantId,
            "ERP produção",
            ToolAuthenticationType.ApiKeyHeader,
            "X-Api-Key",
            "plain-secret"));

        Assert.True(result.Succeeded);
        ToolCredential stored = await provider.GetRequiredService<OrizonAgentsDbContext>()
            .ToolCredentials.AsNoTracking().SingleAsync();
        Assert.NotEqual("plain-secret", stored.EncryptedSecret);
        Assert.Equal(
            "plain-secret",
            provider.GetRequiredService<IToolCredentialProtector>().Unprotect(stored.EncryptedSecret));
    }

    [Fact]
    public async Task RotateSecretAsync_ReplacesProtectedSecret()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantId = SetTenant(provider);
        var service = CreateService(provider);
        var created = await service.CreateAsync(new CreateToolCredentialRequest(
            tenantId,
            "API externa",
            ToolAuthenticationType.BearerToken,
            null,
            "old-secret"));

        var result = await service.RotateSecretAsync(created.Value, "new-secret");

        Assert.True(result.Succeeded);
        var resolved = await service.ResolveForExecutionAsync(created.Value, tenantId);
        Assert.NotNull(resolved);
        Assert.Equal("new-secret", resolved.Secret);
    }

    [Fact]
    public async Task ResolveForExecutionAsync_InactiveCredential_ReturnsNull()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantId = SetTenant(provider);
        var service = CreateService(provider);
        var created = await service.CreateAsync(new CreateToolCredentialRequest(
            tenantId,
            "API externa",
            ToolAuthenticationType.BearerToken,
            null,
            "secret-value"));
        await service.SetActiveAsync(created.Value, false);

        Assert.Null(await service.ResolveForExecutionAsync(created.Value, tenantId));
    }

    [Fact]
    public async Task ResolveForExecutionAsync_MissingCredential_ReturnsNull()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantId = SetTenant(provider);

        Assert.Null(await CreateService(provider).ResolveForExecutionAsync(Guid.NewGuid(), tenantId));
    }

    [Fact]
    public async Task ResolveForExecutionAsync_DifferentTenant_ReturnsNull()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid currentTenantId = SetTenant(provider);
        Guid otherTenantId = Guid.NewGuid();
        var protector = provider.GetRequiredService<IToolCredentialProtector>();
        var credential = new ToolCredential(
            otherTenantId,
            "Outro tenant",
            ToolAuthenticationType.ApiKeyHeader,
            "X-Api-Key",
            protector.Protect("other-secret"));
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        db.ToolCredentials.Add(credential);
        await db.SaveChangesAsync();

        Assert.Null(await CreateService(provider).ResolveForExecutionAsync(credential.Id, currentTenantId));
    }

    [Fact]
    public async Task QueryFilter_IsolatesCredentialsByCurrentTenant()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantA = SetTenant(provider);
        Guid tenantB = Guid.NewGuid();
        var protector = provider.GetRequiredService<IToolCredentialProtector>();
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        db.ToolCredentials.AddRange(
            new ToolCredential(tenantA, "Tenant A", ToolAuthenticationType.BearerToken, null, protector.Protect("a")),
            new ToolCredential(tenantB, "Tenant B", ToolAuthenticationType.BearerToken, null, protector.Protect("b")));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        ToolCredential[] visible = await db.ToolCredentials.AsNoTracking().ToArrayAsync();

        Assert.Single(visible);
        Assert.Equal(tenantA, visible[0].TenantId);
    }

    [Fact]
    public void BearerToken_RejectsNonAuthorizationHeader()
    {
        Assert.Throws<ArgumentException>(() => new ToolCredential(
            Guid.NewGuid(),
            "Bearer inválido",
            ToolAuthenticationType.BearerToken,
            "X-Custom-Auth",
            "protected-value"));
    }

    private static ToolCredentialService CreateService(ServiceProvider provider) =>
        new(
            provider.GetRequiredService<OrizonAgentsDbContext>(),
            provider.GetRequiredService<ICurrentTenant>(),
            provider.GetRequiredService<IToolCredentialProtector>());

    private static Guid SetTenant(ServiceProvider provider)
    {
        Guid tenantId = Guid.NewGuid();
        provider.GetRequiredService<ITenantContextSetter>().SetTenantId(tenantId);
        return tenantId;
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(x => x.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(x => x.GetRequiredService<CurrentTenant>());
        services.AddScoped<IToolCredentialProtector, DataProtectionToolCredentialProtector>();
        services.AddDbContext<OrizonAgentsDbContext>(options =>
            options.UseInMemoryDatabase($"ToolCredentials-{Guid.NewGuid()}"));
        return services.BuildServiceProvider();
    }
}
