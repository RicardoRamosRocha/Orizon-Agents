using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools;

public sealed class AgentToolService
    (OrizonAgentsDbContext dbContext, IGoogleOAuthCapabilityService capabilities) : IAgentToolService
{
    private const string GmailConnectionUnavailable =
        "A conexão Google selecionada não está disponível para leitura do Gmail.";
    private readonly OrizonAgentsDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<AgentToolListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AgentTools
            .AsNoTracking()
            .OrderBy(tool => tool.Name)
            .Select(tool => new AgentToolListItemDto(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                tool.Endpoint,
                tool.IsActive,
                tool.ToolCredential != null ? tool.ToolCredential.Name : null,
                tool.RiskLevel,
                tool.Kind))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AgentToolDetailsDto?> GetAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AgentTools
            .AsNoTracking()
            .Where(tool => tool.Id == toolId)
            .Select(tool => new AgentToolDetailsDto(
                tool.Id,
                tool.TenantId,
                tool.Name,
                tool.Description,
                tool.Endpoint,
                tool.HttpMethod,
                tool.InputSchema,
                tool.IsActive,
                tool.CreatedAtUtc,
                tool.UpdatedAtUtc,
                tool.ToolCredentialId,
                tool.RiskLevel,
                tool.Kind,
                tool.IntegrationConnectionId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateAgentToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            return OperationResult<Guid>.Failure(
                "Tenant é obrigatório.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            return OperationResult<Guid>.Failure("Tipo de Tool inválido.");
        }

        bool isGmail = request.Kind is AgentToolKind.GmailSearch or AgentToolKind.GmailReadMessage;
        if (isGmail && !await IsEligibleGmailConnectionAsync(
                request.TenantId, request.IntegrationConnectionId, cancellationToken))
        {
            return OperationResult<Guid>.Failure(GmailConnectionUnavailable);
        }
        if (!isGmail && request.IntegrationConnectionId.HasValue)
        {
            return OperationResult<Guid>.Failure("Tools HTTP não utilizam conexão Google.");
        }

        if (!isGmail && request.ToolCredentialId.HasValue &&
            !await _dbContext.ToolCredentials.AnyAsync(
                credential =>
                    credential.Id == request.ToolCredentialId.Value &&
                    credential.TenantId == request.TenantId,
                cancellationToken))
        {
            return OperationResult<Guid>.Failure(
                "Credencial de Tool não encontrada para o tenant.");
        }

        try
        {
            string endpoint = isGmail ? GmailEndpoint(request.Kind) : request.Endpoint;
            string httpMethod = isGmail ? "GET" : request.HttpMethod;
            string? inputSchema = isGmail ? null : request.InputSchema;
            Guid? credentialId = isGmail ? null : request.ToolCredentialId;
            var tool = new AgentTool(
                request.TenantId,
                request.Name,
                request.Description,
                endpoint,
                httpMethod);

            tool.Update(
                request.Name,
                request.Description,
                endpoint,
                httpMethod,
                inputSchema,
                credentialId,
                request.RiskLevel);

            if (isGmail)
            {
                tool.ConfigureKind(request.Kind, request.IntegrationConnectionId);
            }

            _dbContext.AgentTools.Add(tool);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<Guid>.Success(tool.Id);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Guid>.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> UpdateAsync(
        UpdateAgentToolRequest request,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.ToolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        bool isGmail = tool.Kind is AgentToolKind.GmailSearch or AgentToolKind.GmailReadMessage;
        if (isGmail && !await IsEligibleGmailConnectionAsync(
                tool.TenantId, request.IntegrationConnectionId, cancellationToken))
        {
            return OperationResult.Failure(GmailConnectionUnavailable);
        }
        if (!isGmail && request.IntegrationConnectionId.HasValue)
        {
            return OperationResult.Failure("Tools HTTP não utilizam conexão Google.");
        }

        if (!isGmail && request.ToolCredentialId.HasValue &&
            !await _dbContext.ToolCredentials.AnyAsync(
                credential =>
                    credential.Id == request.ToolCredentialId.Value &&
                    credential.TenantId == tool.TenantId,
                cancellationToken))
        {
            return OperationResult.Failure(
                "Credencial de Tool não encontrada para o tenant.");
        }

        try
        {
            string endpoint = isGmail ? GmailEndpoint(tool.Kind) : request.Endpoint;
            string httpMethod = isGmail ? "GET" : request.HttpMethod;
            tool.Update(
                request.Name,
                request.Description,
                endpoint,
                httpMethod,
                isGmail ? null : request.InputSchema,
                isGmail ? null : request.ToolCredentialId,
                request.RiskLevel);

            if (isGmail)
            {
                tool.ConfigureKind(tool.Kind, request.IntegrationConnectionId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> ActivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        tool.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private async Task<bool> IsEligibleGmailConnectionAsync(
        Guid tenantId,
        Guid? connectionId,
        CancellationToken cancellationToken)
    {
        if (!connectionId.HasValue || connectionId == Guid.Empty)
        {
            return false;
        }

        bool eligible = await _dbContext.IntegrationConnections.AsNoTracking().AnyAsync(
            connection => connection.Id == connectionId.Value &&
                connection.TenantId == tenantId &&
                connection.Provider == IntegrationProvider.Gmail &&
                connection.IsActive &&
                connection.Status == IntegrationConnectionStatus.Connected,
            cancellationToken);
        if (!eligible)
        {
            return false;
        }

        try
        {
            return await capabilities.HasCapabilityAsync(
                connectionId.Value,
                GoogleOAuthCapability.GmailRead,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static string GmailEndpoint(AgentToolKind kind) => kind switch
    {
        AgentToolKind.GmailSearch => "gmail://messages/search",
        AgentToolKind.GmailReadMessage => "gmail://messages/read",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async Task<OperationResult> DeactivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        tool.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<AgentToolBindingDto>> ListForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            return Array.Empty<AgentToolBindingDto>();
        }

        Guid? tenantId = await _dbContext.AiAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Select(agent => (Guid?)agent.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantId is null)
        {
            return Array.Empty<AgentToolBindingDto>();
        }

        return await (
            from tool in _dbContext.AgentTools.AsNoTracking()
            where tool.TenantId == tenantId.Value
            join binding in _dbContext.AgentToolBindings.AsNoTracking()
                    .Where(candidate =>
                        candidate.AgentId == agentId &&
                        candidate.TenantId == tenantId.Value)
                on tool.Id equals binding.ToolId
                into bindings
            from binding in bindings.DefaultIfEmpty()
            orderby tool.Name
            select new AgentToolBindingDto(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                binding != null && binding.IsActive,
                tool.IsActive,
                tool.RiskLevel))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult> BindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AiAgent? agent = await _dbContext.AiAgents
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult.Failure(
                "Agente não encontrado.");
        }

        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        if (tool.TenantId != agent.TenantId)
        {
            return OperationResult.Failure(
                "A Tool não pertence ao mesmo tenant do agente.");
        }

        if (!tool.IsActive)
        {
            return OperationResult.Failure(
                "Não é possível vincular uma Tool desativada.");
        }

        AgentToolBinding? binding =
            await _dbContext.AgentToolBindings
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.AgentId == agentId &&
                        candidate.ToolId == toolId,
                    cancellationToken);

        if (binding is null)
        {
            binding = new AgentToolBinding(
                agent.TenantId,
                agent.Id,
                tool.Id);

            _dbContext.AgentToolBindings.Add(binding);
        }
        else
        {
            binding.Activate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> UnbindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentToolBinding? binding =
            await _dbContext.AgentToolBindings
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.AgentId == agentId &&
                        candidate.ToolId == toolId,
                    cancellationToken);

        if (binding is null)
        {
            return OperationResult.Failure(
                "Vínculo da Tool com o agente não encontrado.");
        }

        binding.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }
}
