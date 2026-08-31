using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Application.Dashboards.Models;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Dashboards;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private const int RecentLimit = 5;
    private const int AgentPreviewLimit = 6;
    private readonly OrizonAgentsDbContext _dbContext;

    public DashboardQueryService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OperationResult<TenantDashboardDto>> GetTenantDashboardAsync(
        Guid tenantId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Where(candidate => candidate.Id == tenantId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
                candidate.Slug,
                Status = candidate.Status.ToString(),
                candidate.Settings.Culture,
                candidate.Settings.TimeZone,
                candidate.Settings.ContactEmail
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return OperationResult<TenantDashboardDto>.Failure("Organização não encontrada.");
        }

        var userSummary = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(user => user.IsActive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var agentSummary = await _dbContext.AiAgents
            .AsNoTracking()
            .Where(agent => agent.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(agent => agent.IsActive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var knowledgeSummary = await _dbContext.KnowledgeBases
            .AsNoTracking()
            .Where(knowledgeBase => knowledgeBase.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(knowledgeBase => knowledgeBase.IsActive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var toolSummary = await _dbContext.AgentTools
            .AsNoTracking()
            .Where(tool => tool.TenantId == tenantId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(tool => tool.IsActive)
            })
            .SingleOrDefaultAsync(cancellationToken);

        int totalUsers = userSummary?.Total ?? 0;
        int activeUsers = userSummary?.Active ?? 0;
        int totalAgents = agentSummary?.Total ?? 0;
        int activeAgents = agentSummary?.Active ?? 0;
        int totalKnowledgeBases = knowledgeSummary?.Total ?? 0;
        int activeKnowledgeBases = knowledgeSummary?.Active ?? 0;
        int totalTools = toolSummary?.Total ?? 0;
        int activeTools = toolSummary?.Active ?? 0;

        var recentUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.FullName)
            .Take(RecentLimit)
            .Select(user => new RecentUserDto(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                null,
                user.IsActive,
                user.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var agentRows = await _dbContext.AiAgents
            .AsNoTracking()
            .Where(agent => agent.TenantId == tenantId)
            .OrderByDescending(agent => agent.IsActive)
            .ThenBy(agent => agent.Name)
            .Take(AgentPreviewLimit)
            .Select(agent => new
            {
                agent.Id,
                agent.Name,
                agent.Provider,
                agent.Model,
                agent.IsActive,
                KnowledgeBaseCount = _dbContext.AgentKnowledgeBindings.Count(
                    binding => binding.TenantId == tenantId && binding.AgentId == agent.Id),
                ToolCount = _dbContext.AgentToolBindings.Count(
                    binding => binding.TenantId == tenantId &&
                               binding.AgentId == agent.Id &&
                               binding.IsActive)
            })
            .ToArrayAsync(cancellationToken);

        var agents = agentRows
            .Select(agent => new DashboardAgentDto(
                agent.Id,
                agent.Name,
                agent.Provider.ToString(),
                agent.Model,
                agent.IsActive,
                agent.KnowledgeBaseCount,
                agent.ToolCount))
            .ToArray();

        bool hasActiveProviderCredential = await _dbContext.AiProviderCredentials
            .AsNoTracking()
            .AnyAsync(
                credential => credential.TenantId == tenantId && credential.IsActive,
                cancellationToken);

        bool hasKnowledgeBinding = await (
            from binding in _dbContext.AgentKnowledgeBindings.AsNoTracking()
            join knowledgeBase in _dbContext.KnowledgeBases.AsNoTracking()
                on binding.KnowledgeBaseId equals knowledgeBase.Id
            where binding.TenantId == tenantId && knowledgeBase.IsActive
            select binding.Id)
            .AnyAsync(cancellationToken);

        bool hasToolBinding = await (
            from binding in _dbContext.AgentToolBindings.AsNoTracking()
            join tool in _dbContext.AgentTools.AsNoTracking()
                on binding.ToolId equals tool.Id
            where binding.TenantId == tenantId && binding.IsActive && tool.IsActive
            select binding.Id)
            .AnyAsync(cancellationToken);

        var whatsAppStatuses = await _dbContext.WhatsAppConnections
            .AsNoTracking()
            .Where(connection => connection.TenantId == tenantId)
            .Select(connection => connection.Status)
            .ToArrayAsync(cancellationToken);

        var checklist = new[]
        {
            new SetupChecklistItemDto(
                "Organização criada",
                "O tenant base existe e está vinculado à conta.",
                true),
            new SetupChecklistItemDto(
                "Perfil do administrador configurado",
                "O administrador possui nome completo cadastrado.",
                await _dbContext.Users.AsNoTracking().AnyAsync(
                    user => user.Id == currentUserId && !string.IsNullOrWhiteSpace(user.FullName),
                    cancellationToken)),
            new SetupChecklistItemDto(
                "Configurações regionais definidas",
                "Cultura e fuso horário estão preenchidos.",
                !string.IsNullOrWhiteSpace(tenant.Culture) && !string.IsNullOrWhiteSpace(tenant.TimeZone)),
            new SetupChecklistItemDto(
                "Contato da organização informado",
                "Há um e-mail de contato cadastrado para a organização.",
                !string.IsNullOrWhiteSpace(tenant.ContactEmail)),
            new SetupChecklistItemDto(
                "Usuário adicional cadastrado",
                "Há pelo menos um usuário além do administrador inicial.",
                totalUsers > 1)
        };

        var metrics = new[]
        {
            new DashboardMetricDto(
                "Agentes",
                totalAgents,
                DescribeActive(activeAgents, "ativo", "ativos"),
                GetMetricTone(totalAgents, activeAgents)),
            new DashboardMetricDto(
                "Conhecimento",
                totalKnowledgeBases,
                DescribeActive(activeKnowledgeBases, "base ativa", "bases ativas"),
                GetMetricTone(totalKnowledgeBases, activeKnowledgeBases)),
            new DashboardMetricDto(
                "Ferramentas",
                totalTools,
                DescribeActive(activeTools, "ativa", "ativas"),
                GetMetricTone(totalTools, activeTools)),
            new DashboardMetricDto(
                "Usuários",
                totalUsers,
                DescribeActive(activeUsers, "ativo", "ativos"),
                GetMetricTone(totalUsers, activeUsers))
        };

        var configurationStates = new[]
        {
            new DashboardConfigurationStateDto(
                "IA Provider",
                hasActiveProviderCredential ? "Configurado" : "Não configurado",
                hasActiveProviderCredential
                    ? "Credencial ativa disponível"
                    : "Nenhuma credencial ativa",
                hasActiveProviderCredential ? "success" : "neutral"),
            new DashboardConfigurationStateDto(
                "Conhecimento / RAG",
                hasKnowledgeBinding
                    ? "Configurado"
                    : activeKnowledgeBases > 0 ? "Disponível" : "Não configurado",
                hasKnowledgeBinding
                    ? "Base ativa vinculada a agente"
                    : activeKnowledgeBases > 0
                        ? "Base ativa aguardando vínculo"
                        : "Nenhuma base ativa",
                hasKnowledgeBinding ? "success" : activeKnowledgeBases > 0 ? "warning" : "neutral"),
            new DashboardConfigurationStateDto(
                "Ferramentas",
                hasToolBinding
                    ? "Configurado"
                    : activeTools > 0 ? "Disponível" : "Não configurado",
                hasToolBinding
                    ? "Tool ativa vinculada a agente"
                    : activeTools > 0
                        ? "Tool ativa aguardando vínculo"
                        : "Nenhuma tool ativa",
                hasToolBinding ? "success" : activeTools > 0 ? "warning" : "neutral"),
            CreateWhatsAppConfigurationState(whatsAppStatuses)
        };

        return OperationResult<TenantDashboardDto>.Success(
            new TenantDashboardDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status,
                tenant.Culture,
                tenant.TimeZone,
                metrics,
                agents,
                configurationStates,
                recentUsers,
                checklist));
    }

    public async Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default)
    {
        int totalTenants = await _dbContext.Tenants.AsNoTracking().CountAsync(cancellationToken);
        int activeTenants = await _dbContext.Tenants.AsNoTracking().CountAsync(
            tenant => tenant.Status == TenantStatus.Active,
            cancellationToken);
        int inactiveTenants = totalTenants - activeTenants;
        int totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        int activeUsers = await _dbContext.Users.AsNoTracking().CountAsync(user => user.IsActive, cancellationToken);
        int activePlans = await _dbContext.SubscriptionPlans.AsNoTracking().CountAsync(
            plan => plan.IsActive && !plan.IsArchived,
            cancellationToken);
        int trialingSubscriptions = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(
            subscription => subscription.Status == SubscriptionStatus.Trialing,
            cancellationToken);

        var recentTenants = await _dbContext.Tenants
            .AsNoTracking()
            .OrderByDescending(tenant => tenant.CreatedAtUtc)
            .ThenBy(tenant => tenant.Name)
            .Take(RecentLimit)
            .Select(tenant => new RecentTenantDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status.ToString(),
                tenant.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var recentUsers = await _dbContext.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.FullName)
            .Take(RecentLimit)
            .Select(user => new RecentUserDto(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                user.Tenant == null ? null : user.Tenant.Name,
                user.IsActive,
                user.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var metrics = new[]
        {
            new DashboardMetricDto("Tenants", totalTenants, "Total de organizações", "primary"),
            new DashboardMetricDto("Tenants ativos", activeTenants, "Organizações em operação", "success"),
            new DashboardMetricDto("Tenants suspensos/inativos", inactiveTenants, "Organizações fora de operação", "warning"),
            new DashboardMetricDto("Usuários", totalUsers, $"{activeUsers} ativos na plataforma", "violet"),
            new DashboardMetricDto("Planos ativos", activePlans, "Planos disponíveis para assinaturas", "primary"),
            new DashboardMetricDto("Trials ativos", trialingSubscriptions, "Assinaturas em período de teste", "success")
        };

        var technicalStatus = new[]
        {
            new SetupChecklistItemDto("PostgreSQL configurado", "DbContext e provider Npgsql registrados.", true),
            new SetupChecklistItemDto("Redis configurado", "Cache distribuído registrado na Infrastructure.", true),
            new SetupChecklistItemDto("Identity configurado", "Autenticação Web MVC ativa.", true),
            new SetupChecklistItemDto("Billing configurado", "Planos, assinaturas e entitlements registrados.", true)
        };

        return new PlatformDashboardDto(metrics, recentTenants, recentUsers, technicalStatus);
    }

    private static string DescribeActive(int count, string singular, string plural)
    {
        return $"{count} {(count == 1 ? singular : plural)}";
    }

    private static string GetMetricTone(int total, int active)
    {
        if (active > 0)
        {
            return "success";
        }

        return total > 0 ? "warning" : "neutral";
    }

    private static DashboardConfigurationStateDto CreateWhatsAppConfigurationState(
        IReadOnlyCollection<WhatsAppConnectionStatus> statuses)
    {
        if (statuses.Contains(WhatsAppConnectionStatus.Active))
        {
            return new DashboardConfigurationStateDto(
                "WhatsApp",
                "Configurado",
                "Conexão validada cadastrada",
                "success");
        }

        return statuses.Count > 0
            ? new DashboardConfigurationStateDto(
                "WhatsApp",
                "Requer atenção",
                "Conexão ainda não está validada",
                "warning")
            : new DashboardConfigurationStateDto(
                "WhatsApp",
                "Não configurado",
                "Nenhuma conexão cadastrada",
                "neutral");
    }
}
