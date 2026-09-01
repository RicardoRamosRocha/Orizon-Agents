using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.API.Contracts.Agents;
using OrizonAgents.API.Controllers;
using OrizonAgents.API.Security;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Integration.Tests.Api;

public class AgentRunEndpointContractTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CredentialId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public void PublicRequest_ExposesOnlyMessage()
    {
        var properties = typeof(RunAgentRequest).GetProperties();

        Assert.Single(properties);
        Assert.Equal(nameof(RunAgentRequest.Message), properties[0].Name);
        Assert.Equal(
            "{\"message\":\"Hello\"}",
            Serialize(new RunAgentRequest("Hello")));
    }

    [Fact]
    public async Task ValidRequest_ReachesRunnerWithOnlyMessageMapped()
    {
        var runner = StubAgentRunner.Success("Resposta");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(runner.WasCalled);
        Assert.Equal(AgentId, runner.AgentId);
        Assert.Equal("Olá", runner.Request!.Message);
        Assert.Null(runner.Request.ConversationId);
        Assert.Null(runner.Request.Context);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NullEmptyOrWhitespaceMessage_Returns400(string? message)
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest(message),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status400BadRequest, "invalid_request");
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task MessageAboveLimit_Returns400()
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest(new string('a', RunAgentRequest.MessageMaxLength + 1)),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status400BadRequest, "invalid_request");
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task AgentClaimDifferentFromRoute_Returns403()
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        var agentService = new StubAiAgentService(CreateAgent(TenantId, isActive: true));
        AgentsController controller = CreateController(
            runner,
            agentService,
            claimAgentId: Guid.NewGuid());

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status403Forbidden, "agent_not_allowed");
        Assert.False(agentService.WasCalled);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task MissingAgentInAuthorizedTenant_Returns404()
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(agent: null));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status404NotFound, "agent_not_found");
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task InactiveAgent_Returns409()
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: false)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status409Conflict, "agent_inactive");
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task AgentFromAnotherTenant_Returns404AndDoesNotExecute()
    {
        var runner = StubAgentRunner.Success("Não deve executar");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(Guid.NewGuid(), isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AssertError(result, StatusCodes.Status404NotFound, "agent_not_found");
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task Success_ReturnsStablePublicContract()
    {
        var runner = StubAgentRunner.Success("Resposta publica");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunAgentResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("Resposta publica", response.Response);
        Assert.Equal(
            "{\"success\":true,\"response\":\"Resposta publica\"}",
            Serialize(response));
    }

    [Fact]
    public async Task UntypedRunnerFailure_ReturnsSanitized500()
    {
        var runner = StubAgentRunner.Failure(
            "Provider key sk-secret falhou com prompt interno.");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AgentApiErrorResponse response = AssertError(
            result,
            StatusCodes.Status500InternalServerError,
            "execution_failed");
        string json = Serialize(response);
        Assert.DoesNotContain("sk-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt interno", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnexpectedRunnerException_ReturnsSanitized500()
    {
        var runner = StubAgentRunner.Throws(new InvalidOperationException(
            "Detalhe interno sensível"));
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        AgentApiErrorResponse response = AssertError(
            result,
            StatusCodes.Status500InternalServerError,
            "internal_error");
        Assert.DoesNotContain(
            "Detalhe interno sensível",
            Serialize(response),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_DoesNotExposeInternalExecutionOrAgentData()
    {
        var runner = StubAgentRunner.Success("Resposta");
        AgentsController controller = CreateController(
            runner,
            new StubAiAgentService(CreateAgent(TenantId, isActive: true)));

        IActionResult result = await controller.Run(
            AgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        string json = Serialize(ok.Value!);
        Assert.DoesNotContain("conversationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("systemPrompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("model", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenantId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("agentId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credentialId", json, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentsController CreateController(
        IAiAgentRunner runner,
        IAiAgentService agentService,
        Guid? claimAgentId = null,
        Guid? claimTenantId = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(OrizonClaimTypes.TenantId, (claimTenantId ?? TenantId).ToString()),
                new Claim(OrizonClaimTypes.CredentialId, CredentialId.ToString()),
                new Claim(OrizonClaimTypes.AgentId, (claimAgentId ?? AgentId).ToString())
            ], AgentApiKeyDefaults.AuthenticationScheme))
        };

        return new AgentsController(
            runner,
            agentService,
            NullLogger<AgentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static AiAgentDetailsDto CreateAgent(Guid tenantId, bool isActive)
    {
        return new AiAgentDetailsDto(
            AgentId,
            tenantId,
            "Agente",
            "Descrição interna",
            "System prompt secreto",
            "Groq",
            "modelo-interno",
            0.7,
            isActive,
            DateTime.UtcNow,
            null);
    }

    private static AgentApiErrorResponse AssertError(
        IActionResult result,
        int statusCode,
        string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        var response = Assert.IsType<AgentApiErrorResponse>(objectResult.Value);
        Assert.False(response.Success);
        Assert.Equal(code, response.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(response.Error.Message));
        return response;
    }

    private static string Serialize(object value)
    {
        return JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed class StubAiAgentService : IAiAgentService
    {
        private readonly AiAgentDetailsDto? _agent;

        public StubAiAgentService(AiAgentDetailsDto? agent)
        {
            _agent = agent;
        }

        public bool WasCalled { get; private set; }

        public Task<AiAgentDetailsDto?> GetAsync(Guid agentId, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_agent);
        }

        public Task<IReadOnlyList<AiAgentListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<Guid>> CreateAsync(CreateAiAgentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> UpdateAsync(UpdateAiAgentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> ActivateAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> DeactivateAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubAgentRunner : IAiAgentRunner
    {
        private readonly Func<Task<OperationResult<AiAgentRunResult>>> _execute;

        private StubAgentRunner(Func<Task<OperationResult<AiAgentRunResult>>> execute)
        {
            _execute = execute;
        }

        public bool WasCalled { get; private set; }

        public Guid AgentId { get; private set; }

        public AgentRunRequest? Request { get; private set; }

        public Task<OperationResult<AiAgentRunResult>> RunAsync(
            Guid agentId,
            AgentRunRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            AgentId = agentId;
            Request = request;
            return _execute();
        }

        public static StubAgentRunner Success(string response)
        {
            return new StubAgentRunner(() => Task.FromResult(
                OperationResult<AiAgentRunResult>.Success(
                    new AiAgentRunResult(Guid.NewGuid(), response))));
        }

        public static StubAgentRunner Failure(string internalError)
        {
            return new StubAgentRunner(() => Task.FromResult(
                OperationResult<AiAgentRunResult>.Failure(internalError)));
        }

        public static StubAgentRunner Throws(Exception exception)
        {
            return new StubAgentRunner(() => Task.FromException<OperationResult<AiAgentRunResult>>(exception));
        }
    }
}
