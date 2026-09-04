using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Validation;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class AgentToolExecutor : IAgentToolExecutor
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IAgentToolInputValidator _inputValidator;
    private readonly IToolExecutionApprovalService _approvalService;
    private readonly HttpAgentToolExecutor _httpExecutor;
    private readonly GmailAgentToolExecutor _gmailExecutor;
    private readonly ILogger<AgentToolExecutor> _logger;

    public AgentToolExecutor(
        OrizonAgentsDbContext dbContext,
        IAgentToolInputValidator inputValidator,
        IToolExecutionApprovalService approvalService,
        HttpAgentToolExecutor httpExecutor,
        GmailAgentToolExecutor gmailExecutor,
        ILogger<AgentToolExecutor> logger)
    {
        _dbContext = dbContext;
        _inputValidator = inputValidator;
        _approvalService = approvalService;
        _httpExecutor = httpExecutor;
        _gmailExecutor = gmailExecutor;
        _logger = logger;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AgentId == Guid.Empty)
        {
            return AgentToolExecutionResult.Failure(
                "AgentId é obrigatório.");
        }

        if (request.ToolId == Guid.Empty)
        {
            return AgentToolExecutionResult.Failure(
                "ToolId é obrigatório.");
        }

        AgentTool? tool = await (
            from candidateTool in _dbContext.AgentTools.AsNoTracking()
            join binding in _dbContext.AgentToolBindings.AsNoTracking()
                on candidateTool.Id equals binding.ToolId
            join agent in _dbContext.AiAgents.AsNoTracking()
                on binding.AgentId equals agent.Id
            where candidateTool.Id == request.ToolId
                  && agent.Id == request.AgentId
                  && candidateTool.TenantId == agent.TenantId
                  && binding.TenantId == agent.TenantId
                  && candidateTool.IsActive
                  && binding.IsActive
            select candidateTool)
            .SingleOrDefaultAsync(cancellationToken);

        if (tool is null)
        {
            return AgentToolExecutionResult.Failure(
                "Tool não encontrada, inativa ou não vinculada ao agente.");
        }

        AgentToolInputValidationResult inputValidation =
            _inputValidator.Validate(
                tool.InputSchema,
                request.Input);

        if (!inputValidation.IsValid)
        {
            _logger.LogWarning(
                "Execução da Tool {ToolId} bloqueada por argumentos inválidos. AgentId: {AgentId}.",
                request.ToolId,
                request.AgentId);

            return AgentToolExecutionResult.Failure(
                "Os argumentos fornecidos para a Tool são inválidos.");
        }

        ToolExecutionAuthorizationResult authorization =
            await _approvalService.AuthorizeAsync(
                request.AgentId,
                tool,
                request.Input,
                cancellationToken);

        if (authorization.Status ==
            ToolExecutionAuthorizationStatus.ApprovalRequired)
        {
            if (!authorization.ApprovalId.HasValue)
            {
                return AgentToolExecutionResult.Failure(
                    "Não foi possível criar a solicitação de aprovação.");
            }

            _logger.LogInformation(
                "Execução da Tool {ToolId} aguardando aprovação humana. AgentId: {AgentId}.",
                request.ToolId,
                request.AgentId);

            return AgentToolExecutionResult.ApprovalRequired(
                authorization.ApprovalId.Value);
        }

        if (authorization.Status ==
            ToolExecutionAuthorizationStatus.Rejected)
        {
            return AgentToolExecutionResult.Rejected(
                authorization.ApprovalId);
        }

        return tool.Kind switch
        {
            AgentToolKind.Http =>
                await _httpExecutor.ExecuteAsync(
                    tool,
                    request,
                    cancellationToken),

            AgentToolKind.GmailSearch or
            AgentToolKind.GmailReadMessage =>
                await _gmailExecutor.ExecuteAsync(
                    tool,
                    request.Input,
                    cancellationToken),

            _ => AgentToolExecutionResult.Failure(
                "O tipo configurado para a Tool não é suportado.")
        };
    }
}
