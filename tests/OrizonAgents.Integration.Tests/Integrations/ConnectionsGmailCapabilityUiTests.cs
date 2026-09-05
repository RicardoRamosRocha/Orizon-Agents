using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Integrations.Requests;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Web.Controllers;
using OrizonAgents.Web.Models.Integrations;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class ConnectionsGmailCapabilityUiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectedGoogleConnection_MapsGmailReadCapabilityToSemanticViewModel(bool granted)
    {
        Guid connectionId = Guid.NewGuid();
        var capabilities = new StubCapabilityService { Granted = granted };
        var controller = CreateController(Connected(connectionId), capabilities);

        var result = await controller.Details(connectionId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ConnectionDetailsViewModel>(view.Model);
        Assert.Equal(granted, model.IsGmailReadAuthorized);
        Assert.Equal(1, capabilities.Calls);
        Assert.Equal(connectionId, capabilities.ConnectionId);
        Assert.Equal(GoogleOAuthCapability.GmailRead, capabilities.Capability);
    }

    [Fact]
    public async Task NonGoogleProvider_DoesNotQueryGmailCapability()
    {
        Guid connectionId = Guid.NewGuid();
        IntegrationConnectionDto connection = Connected(connectionId) with
        {
            Provider = (IntegrationProvider)999
        };
        var capabilities = new StubCapabilityService { Exception = new InvalidOperationException("must not run") };
        var controller = CreateController(connection, capabilities);

        var result = await controller.Details(connectionId, CancellationToken.None);

        var model = Assert.IsType<ConnectionDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsGmailReadAuthorized);
        Assert.Equal(0, capabilities.Calls);
    }

    [Theory]
    [InlineData(IntegrationConnectionStatus.PendingConfiguration, true)]
    [InlineData(IntegrationConnectionStatus.Disconnected, true)]
    [InlineData(IntegrationConnectionStatus.Error, true)]
    [InlineData(IntegrationConnectionStatus.Connected, false)]
    public async Task UnavailableGoogleConnection_DoesNotOfferGmailRead(
        IntegrationConnectionStatus status,
        bool active)
    {
        Guid connectionId = Guid.NewGuid();
        IntegrationConnectionDto connection = Connected(connectionId) with
        {
            Status = status,
            IsActive = active
        };
        var capabilities = new StubCapabilityService { Granted = true };
        var controller = CreateController(connection, capabilities);

        var result = await controller.Details(connectionId, CancellationToken.None);

        var model = Assert.IsType<ConnectionDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsGmailReadAuthorized);
        Assert.Equal(0, capabilities.Calls);
    }

    [Fact]
    public async Task CapabilityFailure_FailsClosedWithoutBreakingPageOrLeakingDetails()
    {
        const string secret = "SENSITIVE-OAUTH-DETAIL";
        Guid connectionId = Guid.NewGuid();
        var capabilities = new StubCapabilityService
        {
            Exception = new InvalidOperationException(secret)
        };
        var logger = new RecordingLogger();
        var controller = CreateController(Connected(connectionId), capabilities, logger);

        var result = await controller.Details(connectionId, CancellationToken.None);

        var model = Assert.IsType<ConnectionDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.IsGmailReadAuthorized);
        Assert.Equal(1, capabilities.Calls);
        Assert.DoesNotContain(secret, string.Join(" ", logger.Messages));
    }

    [Fact]
    public void UpgradeForm_IsMinimalPostWithAntiforgeryAndNoOAuthInternals()
    {
        string root = FindSolutionRoot();
        string view = File.ReadAllText(Path.Combine(
            root, "src", "OrizonAgents.Web", "Views", "Connections", "Details.cshtml"));

        Assert.Contains("asp-action=\"UpgradeGmailRead\"", view);
        Assert.Contains("method=\"post\"", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("Permitir leitura do Gmail", view);
        Assert.Contains("Leitura do Gmail", view);
        Assert.Contains("Não autorizada", view);
        Assert.Contains("Autorizada", view);
        Assert.DoesNotContain("gmail.readonly", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GoogleOAuthCapability", view);
        Assert.DoesNotContain("IntegrationConnectionId", view);
        Assert.DoesNotContain("name=\"scope\"", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"capability\"", view, StringComparison.OrdinalIgnoreCase);

        string[] viewModelProperties = typeof(ConnectionDetailsViewModel)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(new[] { "Connection", "Edit", "IsGmailReadAuthorized" }, viewModelProperties);

        string serialized = JsonSerializer.Serialize(new ConnectionDetailsViewModel
        {
            Connection = Connected(Guid.NewGuid()),
            IsGmailReadAuthorized = false
        });
        Assert.DoesNotContain("Scope", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credentials", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static ConnectionsController CreateController(
        IntegrationConnectionDto connection,
        StubCapabilityService capabilities,
        ILogger<ConnectionsController>? logger = null) =>
        new(new StubConnectionService(connection), capabilities, logger ?? new RecordingLogger());

    private static IntegrationConnectionDto Connected(Guid id) =>
        new(
            id,
            "Conta Google",
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

    private sealed class StubCapabilityService : IGoogleOAuthCapabilityService
    {
        public bool Granted { get; init; }
        public Exception? Exception { get; init; }
        public int Calls { get; private set; }
        public Guid? ConnectionId { get; private set; }
        public GoogleOAuthCapability? Capability { get; private set; }

        public Task<bool> HasCapabilityAsync(
            Guid connectionId,
            GoogleOAuthCapability capability,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            ConnectionId = connectionId;
            Capability = capability;
            return Exception is null
                ? Task.FromResult(Granted)
                : Task.FromException<bool>(Exception);
        }
    }

    private sealed class StubConnectionService(IntegrationConnectionDto connection) : IIntegrationConnectionService
    {
        public Task<IntegrationConnectionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IntegrationConnectionDto?>(id == connection.Id ? connection : null);

        public Task<IReadOnlyList<IntegrationConnectionDto>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult<Guid>> CreateAsync(CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> UpdateAsync(Guid id, UpdateIntegrationConnectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger<ConnectionsController>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
