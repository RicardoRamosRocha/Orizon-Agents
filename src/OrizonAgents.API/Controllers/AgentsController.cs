using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrizonAgents.API.Contracts.Agents;
using OrizonAgents.API.Security;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.API.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController : ControllerBase
{
    private const int MaxRequestBytes = 256 * 1024;

    private readonly IAiAgentRunner _aiAgentRunner;
    private readonly IAiAgentService _aiAgentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        IAiAgentRunner aiAgentRunner,
        IAiAgentService aiAgentService,
        ILogger<AgentsController> logger)
    {
        _aiAgentRunner = aiAgentRunner;
        _aiAgentService = aiAgentService;
        _logger = logger;
    }

    [HttpPost("{agentId:guid}/run")]
    [Authorize(Policy = AgentApiKeyDefaults.AuthorizationPolicy)]
    [EnableRateLimiting(AgentApiRateLimitDefaults.PolicyName)]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Run(
        Guid agentId,
        [FromBody] RunAgentRequest request,
        CancellationToken cancellationToken)
    {
        string? authenticatedAgentId = User.FindFirstValue(
            OrizonClaimTypes.AgentId);

        if (!Guid.TryParse(authenticatedAgentId, out Guid credentialAgentId) ||
            credentialAgentId != agentId)
        {
            return Error(
                StatusCodes.Status403Forbidden,
                "agent_not_allowed",
                "A credencial não permite executar este agente.");
        }

        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "A mensagem é obrigatória.");
        }

        if (request.Message.Length > RunAgentRequest.MessageMaxLength)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "A mensagem excede o limite permitido.");
        }

        if (!Guid.TryParse(
                User.FindFirstValue(OrizonClaimTypes.TenantId),
                out Guid tenantId))
        {
            return Error(
                StatusCodes.Status403Forbidden,
                "agent_not_allowed",
                "A credencial não permite executar este agente.");
        }

        AiAgentDetailsDto? agent;
        try
        {
            agent = await _aiAgentService.GetAsync(
                agentId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            LogExternalRequestFailure(agentId, tenantId);
            return Error(
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Não foi possível executar o agente.");
        }

        if (agent is null || agent.TenantId != tenantId)
        {
            return Error(
                StatusCodes.Status404NotFound,
                "agent_not_found",
                "Agente não encontrado.");
        }

        if (!agent.IsActive)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "agent_inactive",
                "O agente está inativo.");
        }

        var internalRequest = new AgentRunRequest(request.Message);

        try
        {
            var result = await _aiAgentRunner.RunAsync(
                agentId,
                internalRequest,
                cancellationToken);

            if (!result.Succeeded || result.Value is null)
            {
                LogExternalRequestFailure(agentId, tenantId);
                return Error(
                    StatusCodes.Status500InternalServerError,
                    "execution_failed",
                    "Não foi possível executar o agente.");
            }

            return Ok(new RunAgentResponse(
                Success: true,
                result.Value.Response));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            LogExternalRequestFailure(agentId, tenantId);
            return Error(
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Não foi possível executar o agente.");
        }
    }

    private ObjectResult Error(
        int statusCode,
        string code,
        string message)
    {
        return StatusCode(
            statusCode,
            new AgentApiErrorResponse(
                Success: false,
                new AgentApiError(code, message)));
    }

    private void LogExternalRequestFailure(Guid agentId, Guid tenantId)
    {
        string? credentialId = User.FindFirstValue(
            OrizonClaimTypes.CredentialId);

        _logger.LogWarning(
            "Falha na execução externa do agente. TenantId: {TenantId}; AgentId: {AgentId}; CredentialId: {CredentialId}; RequestId: {RequestId}",
            tenantId,
            agentId,
            credentialId,
            HttpContext.TraceIdentifier);
    }
}
