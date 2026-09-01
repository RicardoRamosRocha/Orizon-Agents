using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Integrations;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Integration.Tests.Authentication;

namespace OrizonAgents.Integration.Tests.Integrations;

public class ApiCredentialServiceTests
{
    [Fact]
    public async Task CreateAsync_GeneratesAgentCredentialWithoutPersistingFullKey()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        (Tenant tenant, AiAgent agent) = await SeedAgentAsync(dbContext);
        var service = new ApiCredentialService(dbContext);

        var created = await service.CreateAsync(tenant.Id, agent.Id, "Produção");

        ApiCredential persisted = await dbContext.ApiCredentials.SingleAsync();
        Assert.Equal(agent.Id, persisted.AgentId);
        Assert.Equal(created.KeyIdentifier, persisted.KeyIdentifier);
        Assert.NotEqual(created.ApiKey, persisted.KeyHash);
        Assert.DoesNotContain(created.ApiKey, persisted.KeyHash, StringComparison.Ordinal);
        Assert.StartsWith($"orizon_{created.KeyIdentifier}.", created.ApiKey, StringComparison.Ordinal);

        var resolved = await service.ResolveAsync(created.ApiKey);
        Assert.NotNull(resolved);
        Assert.Equal(agent.Id, resolved.AgentId);
    }

    [Fact]
    public async Task CreateAsync_RejectsAgentFromAnotherTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        (Tenant tenant, _) = await SeedAgentAsync(dbContext);
        (Tenant otherTenant, AiAgent otherAgent) = await SeedAgentAsync(dbContext, "Tenant B", "tenant-b");
        var service = new ApiCredentialService(dbContext);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(tenant.Id, otherAgent.Id, "Inválida"));

        Assert.Contains("não existe", exception.Message);
        Assert.NotEqual(tenant.Id, otherTenant.Id);
    }

    [Fact]
    public async Task CreateAsync_RejectsNewTenantWideCredential()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = new ApiCredentialService(provider.GetRequiredService<OrizonAgentsDbContext>());

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.CreateAsync(Guid.NewGuid(), "Legada"));
    }

    [Fact]
    public async Task RevokeAsync_MakesCredentialUnresolvable()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        (Tenant tenant, AiAgent agent) = await SeedAgentAsync(dbContext);
        var service = new ApiCredentialService(dbContext);
        var created = await service.CreateAsync(tenant.Id, agent.Id, "Produção");

        await service.RevokeAsync(tenant.Id, created.Id);

        ApiCredential persisted = await dbContext.ApiCredentials.SingleAsync();
        Assert.False(persisted.IsActive);
        Assert.NotNull(persisted.RevokedAtUtc);
        Assert.Null(await service.ResolveAsync(created.ApiKey));
    }

    [Fact]
    public async Task RegenerateAsync_RevokesCurrentCredentialAndCreatesReplacement()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        (Tenant tenant, AiAgent agent) = await SeedAgentAsync(dbContext);
        var service = new ApiCredentialService(dbContext);
        var original = await service.CreateAsync(tenant.Id, agent.Id, "Produção");

        var replacement = await service.RegenerateAsync(tenant.Id, original.Id);

        ApiCredential originalPersisted = await dbContext.ApiCredentials.SingleAsync(x => x.Id == original.Id);
        Assert.False(originalPersisted.IsActive);
        Assert.NotNull(originalPersisted.RevokedAtUtc);
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.NotEqual(original.KeyIdentifier, replacement.KeyIdentifier);
        Assert.Null(await service.ResolveAsync(original.ApiKey));
        Assert.Equal(replacement.Id, (await service.ResolveAsync(replacement.ApiKey))!.Id);
    }

    [Fact]
    public async Task ResolveAsync_RejectsLegacyTenantWideKeyFormat()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = new ApiCredentialService(provider.GetRequiredService<OrizonAgentsDbContext>());

        Assert.Null(await service.ResolveAsync("orizon_legacyTenantWideKey"));
    }

    [Fact]
    public void Model_HasAgentRelationshipAndRequiredIndexes()
    {
        using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        IEntityType entity = dbContext.Model.FindEntityType(typeof(ApiCredential))!;

        Assert.Contains(entity.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(AiAgent) &&
            foreignKey.Properties.Single().Name == nameof(ApiCredential.AgentId));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(ApiCredential.TenantId),
                nameof(ApiCredential.AgentId)
            }));
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name == nameof(ApiCredential.KeyIdentifier));
    }

    private static async Task<(Tenant Tenant, AiAgent Agent)> SeedAgentAsync(
        OrizonAgentsDbContext dbContext,
        string tenantName = "Tenant A",
        string tenantSlug = "tenant-a")
    {
        Tenant tenant = Tenant.Create(tenantName, tenantSlug);
        var agent = new AiAgent(
            tenant.Id,
            "Agente",
            "Você é um agente.",
            AiProvider.OpenAI,
            "gpt-test");

        dbContext.Tenants.Add(tenant);
        dbContext.AiAgents.Add(agent);
        await dbContext.SaveChangesAsync();
        return (tenant, agent);
    }
}
