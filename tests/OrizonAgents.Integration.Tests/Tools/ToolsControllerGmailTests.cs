using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Integrations.Requests;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Web.Controllers;
using OrizonAgents.Web.Models.Tools;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolsControllerGmailTests
{
    [Fact]
    public async Task Create_HttpPreservesExistingFieldsAndDoesNotUseGoogleConnection()
    {
        var tools = new RecordingAgentToolService();
        var controller = CreateController(tools);

        await controller.Create(new AgentToolFormViewModel
        {
            Category = AgentToolCategory.Http,
            Name = "API comercial",
            Description = "Consulta a API.",
            Endpoint = "https://example.com/orders",
            HttpMethod = "POST",
            InputSchema = "{\"type\":\"object\"}",
            ToolCredentialId = Guid.NewGuid(),
            RiskLevel = AgentToolRiskLevel.Write,
            IntegrationConnectionId = Guid.NewGuid()
        }, CancellationToken.None);

        CreateAgentToolRequest request = Assert.IsType<CreateAgentToolRequest>(tools.CreateRequest);
        Assert.Equal(AgentToolKind.Http, request.Kind);
        Assert.Equal("https://example.com/orders", request.Endpoint);
        Assert.Equal("POST", request.HttpMethod);
        Assert.NotNull(request.InputSchema);
        Assert.NotNull(request.ToolCredentialId);
        Assert.Null(request.IntegrationConnectionId);
    }

    [Theory]
    [InlineData(GmailToolAction.SearchEmails, AgentToolKind.GmailSearch)]
    [InlineData(GmailToolAction.ReadEmail, AgentToolKind.GmailReadMessage)]
    public async Task Create_GmailMapsSemanticActionThroughServerAllowlist(
        GmailToolAction action,
        AgentToolKind expectedKind)
    {
        var tools = new RecordingAgentToolService();
        var controller = CreateController(tools);
        Guid connectionId = Guid.NewGuid();

        await controller.Create(new AgentToolFormViewModel
        {
            Category = AgentToolCategory.Gmail,
            GmailAction = action,
            IntegrationConnectionId = connectionId,
            Name = "Gmail",
            Description = "Executa uma ação Gmail.",
            Endpoint = "https://attacker.invalid",
            HttpMethod = "DELETE",
            InputSchema = "sensitive-client-value",
            ToolCredentialId = Guid.NewGuid(),
            RiskLevel = AgentToolRiskLevel.Read
        }, CancellationToken.None);

        CreateAgentToolRequest request = Assert.IsType<CreateAgentToolRequest>(tools.CreateRequest);
        Assert.Equal(expectedKind, request.Kind);
        Assert.Equal(connectionId, request.IntegrationConnectionId);
    }

    [Fact]
    public async Task Create_InvalidGmailActionIsRejectedBeforeService()
    {
        var tools = new RecordingAgentToolService();
        var controller = CreateController(tools);

        IActionResult result = await controller.Create(new AgentToolFormViewModel
        {
            Category = AgentToolCategory.Gmail,
            GmailAction = (GmailToolAction)999,
            IntegrationConnectionId = Guid.NewGuid(),
            Name = "Gmail",
            Description = "Ação inválida."
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(tools.CreateRequest);
        Assert.True(controller.ModelState.ContainsKey(nameof(AgentToolFormViewModel.GmailAction)));
    }

    [Fact]
    public async Task Create_GetListsOnlyConnectedActiveGmailReadConnections()
    {
        Guid eligibleId = Guid.NewGuid();
        Guid missingCapabilityId = Guid.NewGuid();
        var connections = new[]
        {
            Connection(eligibleId, "Conta autorizada"),
            Connection(missingCapabilityId, "Sem Gmail"),
            Connection(Guid.NewGuid(), "Desconectada") with { Status = IntegrationConnectionStatus.Disconnected },
            Connection(Guid.NewGuid(), "Inativa") with { IsActive = false },
            Connection(Guid.NewGuid(), "Outro provedor") with { Provider = (IntegrationProvider)999 }
        };
        var capabilities = new StubCapabilityService(eligibleId);
        var controller = CreateController(
            new RecordingAgentToolService(),
            connections,
            capabilities);

        var result = Assert.IsType<ViewResult>(await controller.Create(CancellationToken.None));
        var model = Assert.IsType<AgentToolFormViewModel>(result.Model);

        var option = Assert.Single(model.GmailConnectionOptions);
        Assert.Equal(eligibleId.ToString(), option.Value);
        Assert.Contains("Conta autorizada", option.Text);
        Assert.DoesNotContain(model.GmailConnectionOptions, item => item.Text.Contains("Sem Gmail"));
        Assert.Equal(2, capabilities.Calls);
        Assert.All(capabilities.RequestedCapabilities,
            capability => Assert.Equal(GoogleOAuthCapability.GmailRead, capability));
    }

    [Fact]
    public void CreateView_UsesSemanticAllowlistAndDoesNotExposeOAuthInternals()
    {
        string root = FindSolutionRoot();
        string view = File.ReadAllText(Path.Combine(
            root, "src", "OrizonAgents.Web", "Views", "Tools", "Create.cshtml"));

        Assert.Contains("Pesquisar e-mails", view);
        Assert.Contains("Ler e-mail", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.DoesNotContain("AgentToolKind", view);
        Assert.DoesNotContain("gmail.readonly", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            nameof(AgentToolKind),
            typeof(AgentToolFormViewModel).GetProperties().Select(property => property.Name));

        string serialized = JsonSerializer.Serialize(new AgentToolFormViewModel());
        Assert.DoesNotContain("Token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OAuth", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scope", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolsController CreateController(
        RecordingAgentToolService tools,
        IReadOnlyList<IntegrationConnectionDto>? connections = null,
        IGoogleOAuthCapabilityService? capabilities = null)
    {
        Guid tenantId = Guid.NewGuid();
        var controller = new ToolsController(
            tools,
            new StubCredentialService(),
            new StubConnectionService(connections ?? []),
            capabilities ?? new StubCapabilityService(),
            NullLogger<ToolsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(OrizonClaimTypes.TenantId, tenantId.ToString())], "test"))
            }
        };
        return controller;
    }

    private static IntegrationConnectionDto Connection(Guid id, string name) => new(
        id,
        name,
        IntegrationProvider.Gmail,
        IntegrationConnectionStatus.Connected,
        true,
        DateTime.UtcNow,
        null,
        "usuario@example.com");

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OrizonAgents.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Raiz da solução não encontrada.");
    }

    private sealed class StubCapabilityService(params Guid[] grantedIds) : IGoogleOAuthCapabilityService
    {
        public int Calls { get; private set; }
        public List<GoogleOAuthCapability> RequestedCapabilities { get; } = [];

        public Task<bool> HasCapabilityAsync(
            Guid connectionId,
            GoogleOAuthCapability capability,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            RequestedCapabilities.Add(capability);
            return Task.FromResult(grantedIds.Contains(connectionId));
        }
    }

    private sealed class StubConnectionService(IReadOnlyList<IntegrationConnectionDto> connections)
        : IIntegrationConnectionService
    {
        public Task<IReadOnlyList<IntegrationConnectionDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(connections);
        public Task<IntegrationConnectionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(connections.SingleOrDefault(connection => connection.Id == id));
        public Task<OperationResult<Guid>> CreateAsync(CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> UpdateAsync(Guid id, UpdateIntegrationConnectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubCredentialService : IToolCredentialService
    {
        public Task<IReadOnlyList<ToolCredentialListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolCredentialListItemDto>>([]);
        public Task<OperationResult<Guid>> CreateAsync(CreateToolCredentialRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> RotateSecretAsync(Guid credentialId, string secret, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> SetActiveAsync(Guid credentialId, bool active, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ResolvedToolCredential?> ResolveForExecutionAsync(Guid credentialId, Guid tenantId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAgentToolService : IAgentToolService
    {
        public CreateAgentToolRequest? CreateRequest { get; private set; }

        public Task<OperationResult<Guid>> CreateAsync(CreateAgentToolRequest request, CancellationToken cancellationToken = default)
        {
            CreateRequest = request;
            return Task.FromResult(OperationResult<Guid>.Failure("Teste encerra antes do redirect."));
        }

        public Task<IReadOnlyList<AgentToolListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AgentToolListItemDto>>([]);
        public Task<AgentToolDetailsDto?> GetAsync(Guid toolId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentToolDetailsDto?>(null);
        public Task<OperationResult> UpdateAsync(UpdateAgentToolRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> ActivateAsync(Guid toolId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> DeactivateAsync(Guid toolId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AgentToolBindingDto>> ListForAgentAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> BindAsync(Guid agentId, Guid toolId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> UnbindAsync(Guid agentId, Guid toolId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
