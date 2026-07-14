using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Application.Dashboards.Models;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Dashboards;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private const int RecentLimit = 5;
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

        int totalUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.TenantId == tenantId, cancellationToken);

        int activeUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(user => user.TenantId == tenantId && user.IsActive, cancellationToken);

        int inactiveUsers = totalUsers - activeUsers;
        int adminUsers = await CountTenantAdminsAsync(tenantId, cancellationToken);

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
            new DashboardMetricDto("Usuários", totalUsers, "Total cadastrado no tenant", "primary"),
            new DashboardMetricDto("Ativos", activeUsers, "Contas habilitadas", "success"),
            new DashboardMetricDto("Inativos", inactiveUsers, "Contas desativadas", "warning"),
            new DashboardMetricDto("Administradores", adminUsers, "TenantAdmins ativos ou inativos", "violet")
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
            new DashboardMetricDto("Usuários", totalUsers, $"{activeUsers} ativos na plataforma", "violet")
        };

        var technicalStatus = new[]
        {
            new SetupChecklistItemDto("PostgreSQL configurado", "DbContext e provider Npgsql registrados.", true),
            new SetupChecklistItemDto("Redis configurado", "Cache distribuído registrado na Infrastructure.", true),
            new SetupChecklistItemDto("Identity configurado", "Autenticação Web MVC ativa.", true)
        };

        return new PlatformDashboardDto(metrics, recentTenants, recentUsers, technicalStatus);
    }

    private async Task<int> CountTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        string normalizedRole = OrizonRoles.TenantAdmin.ToUpperInvariant();

        return await (
            from user in _dbContext.Users.AsNoTracking()
            join userRole in _dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.TenantId == tenantId && role.NormalizedName == normalizedRole
            select user.Id)
            .CountAsync(cancellationToken);
    }
}
