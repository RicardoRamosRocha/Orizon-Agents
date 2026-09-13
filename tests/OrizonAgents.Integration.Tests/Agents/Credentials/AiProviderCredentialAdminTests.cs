using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge;
using OrizonAgents.Application.Tools;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Agents.Credentials;
using OrizonAgents.Infrastructure.Agents.Credentials;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Web.Controllers;
using OrizonAgents.Web.Models.Agents;
using OrizonAgents.Web.Models.AiCredentials;

namespace OrizonAgents.Integration.Tests.Agents.Credentials;

public sealed class AiProviderCredentialAdminTests
{
    [Fact]
    public async Task OpenAiCredential_CanBeSavedReplacedRemovedAndIsTenantIsolated()
    {
        var tenant = new CurrentTenant();
        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new OrizonAgentsDbContext(options, tenant);
        var service = new AiProviderCredentialService(
            dbContext,
            tenant,
            new TestProtector());
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        tenant.SetTenantId(tenantA);
        await service.SaveAsync(AiProvider.OpenAI, "  sk-tenant-a-v1  ");

        Assert.True(await service.HasCredentialAsync(AiProvider.OpenAI));
        Assert.Equal(
            "sk-tenant-a-v1",
            await service.ResolveAsync(AiProvider.OpenAI));

        AiProviderCredential persisted = await dbContext.AiProviderCredentials
            .IgnoreQueryFilters()
            .SingleAsync();
        Assert.DoesNotContain(
            "sk-tenant-a-v1",
            persisted.EncryptedApiKey,
            StringComparison.Ordinal);

        await service.SaveAsync(AiProvider.OpenAI, "sk-tenant-a-v2");
        Assert.Equal(
            "sk-tenant-a-v2",
            await service.ResolveAsync(AiProvider.OpenAI));
        Assert.Single(await dbContext.AiProviderCredentials
            .IgnoreQueryFilters()
            .ToListAsync());

        tenant.SetTenantId(tenantB);
        Assert.False(await service.HasCredentialAsync(AiProvider.OpenAI));
        Assert.Null(await service.ResolveAsync(AiProvider.OpenAI));
        await service.SaveAsync(AiProvider.OpenAI, "sk-tenant-b");

        tenant.SetTenantId(tenantA);
        Assert.Equal(
            "sk-tenant-a-v2",
            await service.ResolveAsync(AiProvider.OpenAI));
        await service.RemoveAsync(AiProvider.OpenAI);
        Assert.False(await service.HasCredentialAsync(AiProvider.OpenAI));
        Assert.Null(await service.ResolveAsync(AiProvider.OpenAI));

        tenant.SetTenantId(tenantB);
        Assert.Equal(
            "sk-tenant-b",
            await service.ResolveAsync(AiProvider.OpenAI));
    }

    [Fact]
    public async Task CredentialsPage_UsesRegisteredProvidersAndNeverReturnsApiKey()
    {
        var credentials = new StubCredentialService(AiProvider.OpenAI);
        var catalog = new StubModelCatalog();
        var controller = new AiCredentialsController(credentials, catalog);

        IActionResult result = await controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AiCredentialsIndexViewModel>(view.Model);
        AiProviderCredentialViewModel openAi = Assert.Single(
            model.Providers,
            item => item.Provider == AiProvider.OpenAI);
        Assert.Equal("OpenAI", openAi.ProviderName);
        Assert.True(openAi.IsConfigured);
        Assert.DoesNotContain(
            typeof(AiProviderCredentialViewModel).GetProperties(),
            property => property.Name.Contains(
                "ApiKey",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CredentialsActions_AcceptOpenAiAndRejectUnregisteredProvider()
    {
        var credentials = new StubCredentialService();
        var controller = new AiCredentialsController(
            credentials,
            new StubModelCatalog())
        {
            TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                new StubTempDataProvider())
        };

        Assert.IsType<RedirectToActionResult>(
            await controller.Save(
                AiProvider.OpenAI,
                "sk-new",
                CancellationToken.None));
        Assert.Equal(
            (AiProvider.OpenAI, "sk-new"),
            Assert.Single(credentials.Saved));

        Assert.IsType<RedirectToActionResult>(
            await controller.Remove(
                AiProvider.OpenAI,
                CancellationToken.None));
        Assert.Equal(
            AiProvider.OpenAI,
            Assert.Single(credentials.Removed));

        Assert.IsType<BadRequestResult>(
            await controller.Save(
                AiProvider.AnthropicClaude,
                "not-supported",
                CancellationToken.None));
        Assert.Single(credentials.Saved);
    }

    [Fact]
    public void AgentForms_RenderOnlyProvidersSuppliedByBackendCatalog()
    {
        string root = FindSolutionRoot();
        foreach (string viewName in new[] { "Create.cshtml", "Edit.cshtml" })
        {
            string view = File.ReadAllText(Path.Combine(
                root,
                "src",
                "OrizonAgents.Web",
                "Views",
                "Agents",
                viewName));

            Assert.Contains("Model.AvailableProviders", view);
            Assert.Contains("@provider.Provider", view);
            Assert.DoesNotContain("AnthropicClaude", view);
        }
    }

    [Fact]
    public async Task CreateAndEditAgent_AcceptOpenAiThroughItsRegisteredCatalog()
    {
        Guid tenantId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        var agents = new StubAiAgentService(agentId);
        var catalog = new StubModelCatalog();
        AgentsController controller = CreateAgentsController(
            agents,
            catalog,
            tenantId);
        var form = new AiAgentFormViewModel
        {
            Name = "Agente OpenAI",
            SystemPrompt = "Responda com objetividade.",
            Provider = AiProvider.OpenAI.ToString(),
            Model = "gpt-test",
            Temperature = 0.4
        };

        Assert.IsType<RedirectToActionResult>(
            await controller.Create(form, CancellationToken.None));
        Assert.Equal(AiProvider.OpenAI, catalog.LastValidatedProvider);
        Assert.Equal(
            AiProvider.OpenAI.ToString(),
            Assert.IsType<CreateAiAgentRequest>(agents.Created).Provider);

        Assert.IsType<RedirectToActionResult>(
            await controller.Edit(agentId, form, CancellationToken.None));
        Assert.Equal(AiProvider.OpenAI, catalog.LastValidatedProvider);
        Assert.Equal(
            AiProvider.OpenAI.ToString(),
            Assert.IsType<UpdateAiAgentRequest>(agents.Updated).Provider);
    }

    private static AgentsController CreateAgentsController(
        IAiAgentService agents,
        IAiProviderModelCatalog catalog,
        Guid tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(OrizonClaimTypes.TenantId, tenantId.ToString())
            ],
                "test"))
        };
        var controller = new AgentsController(
            agents,
            Unused<IAiAgentRunner>(),
            Unused<IAiConversationService>(),
            Unused<IAgentToolService>(),
            Unused<IKnowledgeService>(),
            catalog)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(
                httpContext,
                new StubTempDataProvider())
        };

        return controller;
    }

    private static T Unused<T>() where T : class =>
        DispatchProxy.Create<T, UnusedServiceProxy>();

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "OrizonAgents.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Raiz da solução não encontrada.");
    }

    private sealed class TestProtector : IAiProviderCredentialProtector
    {
        public string Protect(string apiKey) =>
            $"protected::{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(apiKey))}";

        public string Unprotect(string encryptedApiKey) =>
            System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    encryptedApiKey["protected::".Length..]));
    }

    private sealed class StubCredentialService(
        params AiProvider[] configuredProviders)
        : IAiProviderCredentialService
    {
        public List<(AiProvider Provider, string ApiKey)> Saved { get; } = [];
        public List<AiProvider> Removed { get; } = [];

        public Task SaveAsync(
            AiProvider provider,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            Saved.Add((provider, apiKey));
            return Task.CompletedTask;
        }

        public Task<string?> ResolveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> HasCredentialAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(configuredProviders.Contains(provider));

        public Task RemoveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            Removed.Add(provider);
            return Task.CompletedTask;
        }
    }

    private sealed class StubModelCatalog : IAiProviderModelCatalog
    {
        public AiProvider? LastValidatedProvider { get; private set; }

        public IReadOnlyList<AiProviderDescriptor> Providers { get; } =
        [
            new(AiProvider.OpenAI, "OpenAI"),
            new(AiProvider.GoogleGemini, "Google Gemini"),
            new(AiProvider.Groq, "Groq")
        ];

        public Task<IReadOnlyList<AiProviderModel>> ListAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsValidAsync(
            AiProvider provider,
            string model,
            CancellationToken cancellationToken = default)
        {
            LastValidatedProvider = provider;
            return Task.FromResult(
                provider == AiProvider.OpenAI &&
                model == "gpt-test");
        }
    }

    private sealed class StubAiAgentService(Guid agentId) : IAiAgentService
    {
        public CreateAiAgentRequest? Created { get; private set; }
        public UpdateAiAgentRequest? Updated { get; private set; }

        public Task<OperationResult<Guid>> CreateAsync(
            CreateAiAgentRequest request,
            CancellationToken cancellationToken = default)
        {
            Created = request;
            return Task.FromResult(OperationResult<Guid>.Success(agentId));
        }

        public Task<OperationResult> UpdateAsync(
            UpdateAiAgentRequest request,
            CancellationToken cancellationToken = default)
        {
            Updated = request;
            return Task.FromResult(OperationResult.Success());
        }

        public Task<IReadOnlyList<AiAgentListItemDto>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AiAgentDetailsDto?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> ActivateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> DeactivateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private class UnusedServiceProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args) =>
            throw new NotSupportedException(
                $"{targetMethod?.Name} não deveria ser chamado neste teste.");
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(
            HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values)
        {
        }
    }
}
