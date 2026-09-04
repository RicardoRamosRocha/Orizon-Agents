using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Integrations.Requests;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Integrations;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class IntegrationConnectionServiceTests
{
    [Fact]
    public async Task Create_AssignsCurrentTenantAndInitialState_AndAllowsMultipleGmailAccounts()
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var service = new IntegrationConnectionService(db, tenant);
        var first = await service.CreateAsync(new("  E-mail Comercial  ", IntegrationProvider.Gmail));
        var second = await service.CreateAsync(new("E-mail Comercial", IntegrationProvider.Gmail));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Value, second.Value);
        db.ChangeTracker.Clear();
        var stored = await db.IntegrationConnections.ToArrayAsync();
        Assert.Equal(2, stored.Length);
        Assert.All(stored, connection =>
        {
            Assert.Equal(tenant.TenantId, connection.TenantId);
            Assert.Equal("E-mail Comercial", connection.Name);
            Assert.Equal(IntegrationProvider.Gmail, connection.Provider);
            Assert.Equal(IntegrationConnectionStatus.PendingConfiguration, connection.Status);
            Assert.True(connection.IsActive);
            Assert.Null(connection.EncryptedCredentials);
            Assert.NotEqual(default, connection.CreatedAtUtc);
            Assert.Equal(DateTimeKind.Utc, connection.CreatedAtUtc.Kind);
            Assert.Null(connection.UpdatedAtUtc);
        });
    }

    [Fact]
    public async Task ListAndGet_EnforceTenantEvenWhenOtherTenantEntityIsTracked()
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var own = new IntegrationConnection(tenant.TenantId!.Value, "Minha conta", IntegrationProvider.Gmail);
        var other = new IntegrationConnection(Guid.NewGuid(), "Outra conta", IntegrationProvider.Gmail);
        db.AddRange(own, other);
        await db.SaveChangesAsync();
        var service = new IntegrationConnectionService(db, tenant);

        Assert.Equal(own.Id, Assert.Single(await service.ListAsync()).Id);
        Assert.NotNull(await service.GetAsync(own.Id));
        Assert.Null(await service.GetAsync(other.Id));
        Assert.Null(await service.GetAsync(Guid.NewGuid()));
        Assert.Equal(own.Id, Assert.Single(await db.IntegrationConnections.AsNoTracking().ToArrayAsync()).Id);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    [InlineData("delete")]
    public async Task Mutations_RejectOtherTenantAndPreserveStoredData(string action)
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var other = new IntegrationConnection(Guid.NewGuid(), "Outra conta", IntegrationProvider.Gmail);
        if (action == "activate")
        {
            other.Deactivate();
        }
        bool initialActive = other.IsActive;
        db.Add(other);
        await db.SaveChangesAsync();
        var service = new IntegrationConnectionService(db, tenant);

        var result = action switch
        {
            "edit" => await service.UpdateAsync(other.Id, new("Invasão")),
            "activate" => await service.SetActiveAsync(other.Id, true),
            "deactivate" => await service.SetActiveAsync(other.Id, false),
            _ => await service.DeleteAsync(other.Id)
        };

        Assert.False(result.Succeeded);
        Assert.Equal("Conexão não encontrada.", Assert.Single(result.Errors));
        db.ChangeTracker.Clear();
        var unchanged = await db.IntegrationConnections.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("Outra conta", unchanged.Name);
        Assert.Equal(initialActive, unchanged.IsActive);
        Assert.Null(unchanged.UpdatedAtUtc);
    }

    [Fact]
    public async Task Owner_CanRenameToggleAndDelete_WithoutChangingAuthenticationStatus()
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var service = new IntegrationConnectionService(db, tenant);
        var created = await service.CreateAsync(new("Original", IntegrationProvider.Gmail));
        Assert.True((await service.UpdateAsync(created.Value, new("  Novo nome  "))).Succeeded);
        Assert.True((await service.SetActiveAsync(created.Value, false)).Succeeded);
        var updated = await service.GetAsync(created.Value);
        Assert.NotNull(updated);
        Assert.Equal("Novo nome", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Equal(IntegrationConnectionStatus.PendingConfiguration, updated.Status);
        Assert.Equal(DateTimeKind.Utc, updated.UpdatedAtUtc!.Value.Kind);
        Assert.True((await service.SetActiveAsync(created.Value, true)).Succeeded);
        Assert.True((await service.GetAsync(created.Value))!.IsActive);
        Assert.Equal(IntegrationConnectionStatus.PendingConfiguration, (await service.GetAsync(created.Value))!.Status);
        Assert.True((await service.DeleteAsync(created.Value)).Succeeded);
        Assert.Null(await service.GetAsync(created.Value));
        Assert.Empty(await service.ListAsync());
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    [InlineData("delete")]
    public async Task Operations_RequireTenant_EvenThoughGlobalFilterAllowsNoTenant(string action)
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var connection = new IntegrationConnection(tenant.TenantId!.Value, "Conta", IntegrationProvider.Gmail);
        db.Add(connection);
        await db.SaveChangesAsync();
        tenant.Clear();
        var service = new IntegrationConnectionService(db, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            switch (action)
            {
                case "list": await service.ListAsync(); break;
                case "get": await service.GetAsync(connection.Id); break;
                case "create": await service.CreateAsync(new("Conta", IntegrationProvider.Gmail)); break;
                case "update": await service.UpdateAsync(connection.Id, new("Alterada")); break;
                case "activate": await service.SetActiveAsync(connection.Id, true); break;
                case "deactivate": await service.SetActiveAsync(connection.Id, false); break;
                case "delete": await service.DeleteAsync(connection.Id); break;
            }
        });
        Assert.Equal("Conta", (await db.IntegrationConnections.SingleAsync()).Name);
    }

    [Fact]
    public async Task InvalidInputs_ReturnFailures_WithoutPersistingChanges()
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var service = new IntegrationConnectionService(db, tenant);
        Assert.False((await service.CreateAsync(new("Conta", (IntegrationProvider)999))).Succeeded);
        Assert.False((await service.CreateAsync(new(" ", IntegrationProvider.Gmail))).Succeeded);
        Assert.Empty(await service.ListAsync());
        var created = await service.CreateAsync(new("Conta", IntegrationProvider.Gmail));
        Assert.False((await service.UpdateAsync(created.Value, new(" "))).Succeeded);
        Assert.Equal("Conta", (await service.GetAsync(created.Value))!.Name);
    }

    [Fact]
    public async Task Credentials_AreProtectedAndBoundToOwnerAndConnection_AndAbsentFromDtos()
    {
        var tenant = CreateTenant();
        await using var db = CreateDb(tenant);
        var connection = new IntegrationConnection(tenant.TenantId!.Value, "Conta", IntegrationProvider.Gmail);
        var protector = new IntegrationConnectionCredentialProtector(new EphemeralDataProtectionProvider());
        const string secret = "test-only-sensitive-payload";
        string encrypted = protector.Protect(connection.TenantId, connection.Id, secret);
        connection.ReplaceProtectedCredentials(encrypted);
        db.Add(connection);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var stored = await db.IntegrationConnections.SingleAsync();
        Assert.NotEqual(secret, stored.EncryptedCredentials);
        Assert.Equal(secret, protector.Unprotect(stored.TenantId, stored.Id, stored.EncryptedCredentials!));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(Guid.NewGuid(), stored.Id, encrypted));
        Assert.Throws<CryptographicException>(() => protector.Unprotect(stored.TenantId, Guid.NewGuid(), encrypted));

        var service = new IntegrationConnectionService(db, tenant);
        string details = JsonSerializer.Serialize(await service.GetAsync(stored.Id));
        string list = JsonSerializer.Serialize(await service.ListAsync());
        Assert.DoesNotContain(secret, details);
        Assert.DoesNotContain(encrypted, details);
        Assert.DoesNotContain(secret, list);
        Assert.DoesNotContain(encrypted, list);
        Assert.Equal(
            new[] { "ConnectedAccountEmail", "CreatedAtUtc", "Id", "IsActive", "Name", "Provider", "Status", "UpdatedAtUtc" },
            typeof(IntegrationConnectionDto).GetProperties().Select(x => x.Name).OrderBy(x => x).ToArray());
        Assert.Equal(
            new[] { "Name", "Provider" },
            typeof(CreateIntegrationConnectionRequest).GetProperties().Select(x => x.Name).OrderBy(x => x).ToArray());
    }

    [Fact]
    public void RelationalModel_HasTenantRelationshipFilterAndNonUniqueIndexes()
    {
        var tenant = CreateTenant();
        using var db = new OrizonAgentsDbContext(
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseNpgsql("Host=localhost;Database=model_tests;Username=test;Password=test").Options, tenant);
        var entity = db.Model.FindEntityType(typeof(IntegrationConnection))!;
        Assert.NotNull(entity.GetQueryFilter());
        Assert.Equal(IntegrationConnection.NameMaxLength, entity.FindProperty("Name")!.GetMaxLength());
        Assert.Equal(IntegrationConnection.EncryptedCredentialsMaxLength, entity.FindProperty("EncryptedCredentials")!.GetMaxLength());
        Assert.True(entity.FindProperty("EncryptedCredentials")!.IsNullable);
        Assert.Equal(320, entity.FindProperty("ConnectedAccountEmail")!.GetMaxLength());
        Assert.Equal(64, entity.FindProperty("PendingOAuthStateHash")!.GetMaxLength());
        Assert.True(entity.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken);
        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Tenant), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal("TenantId", Assert.Single(fk.Properties).Name);
        var indexes = entity.GetIndexes().ToArray();
        Assert.Contains(indexes, x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Provider" }) && !x.IsUnique);
        Assert.Contains(indexes, x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Name" }) && !x.IsUnique);
    }

    private static CurrentTenant CreateTenant()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenantId(Guid.NewGuid());
        return tenant;
    }

    private static OrizonAgentsDbContext CreateDb(CurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase($"IntegrationConnections-{Guid.NewGuid()}").Options, tenant);
}