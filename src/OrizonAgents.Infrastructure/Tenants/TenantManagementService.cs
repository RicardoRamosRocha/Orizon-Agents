using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Dashboards.Models;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tenants;

public sealed class TenantManagementService : ITenantManagementService
{
    private const int RecentUsersLimit = 5;
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantManagementService(
        OrizonAgentsDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<PagedResult<TenantListItemDto>> ListAsync(
        TenantListRequest request,
        CancellationToken cancellationToken = default)
    {
        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 5, 50);
        string? search = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : request.Search.Trim().ToLowerInvariant();

        IQueryable<Tenant> query = _dbContext.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(tenant =>
                tenant.Name.ToLower().Contains(search) ||
                tenant.Slug.ToLower().Contains(search));
        }

        if (Enum.TryParse(request.Status, ignoreCase: true, out TenantStatus status))
        {
            query = query.Where(tenant => tenant.Status == status);
        }

        int totalItems = await query.CountAsync(cancellationToken);

        query = request.Sort?.ToLowerInvariant() switch
        {
            "name_desc" => query.OrderByDescending(tenant => tenant.Name),
            "slug" => query.OrderBy(tenant => tenant.Slug),
            "slug_desc" => query.OrderByDescending(tenant => tenant.Slug),
            "status" => query.OrderBy(tenant => tenant.Status).ThenBy(tenant => tenant.Name),
            "created" => query.OrderBy(tenant => tenant.CreatedAtUtc),
            _ => query.OrderByDescending(tenant => tenant.CreatedAtUtc)
        };

        TenantListItemDto[] items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(tenant => new TenantListItemDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status.ToString(),
                _dbContext.Users.Count(user => user.TenantId == tenant.Id),
                tenant.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<TenantListItemDto>(items, pageNumber, pageSize, totalItems);
    }

    public async Task<TenantDetailsDto?> GetDetailsAsync(
        Guid tenantId,
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
                candidate.SuspensionReason,
                candidate.SuspendedAtUtc,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc,
                candidate.ConcurrencyStamp,
                candidate.Settings.Culture,
                candidate.Settings.TimeZone,
                candidate.Settings.ContactName,
                candidate.Settings.ContactEmail,
                candidate.Settings.ContactPhone
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        int totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(
            user => user.TenantId == tenantId,
            cancellationToken);
        int activeUsers = await _dbContext.Users.AsNoTracking().CountAsync(
            user => user.TenantId == tenantId && user.IsActive,
            cancellationToken);
        int adminUsers = await CountTenantAdminsAsync(tenantId, cancellationToken);
        RecentUserDto[] recentUsers = await GetRecentUsersAsync(tenantId, cancellationToken);

        return new TenantDetailsDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.Culture,
            tenant.TimeZone,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.SuspensionReason,
            tenant.SuspendedAtUtc,
            tenant.CreatedAtUtc,
            tenant.UpdatedAtUtc,
            tenant.ConcurrencyStamp,
            totalUsers,
            activeUsers,
            adminUsers,
            recentUsers);
    }

    public async Task<TenantOrganizationDto?> GetOrganizationAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new TenantOrganizationDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.Status.ToString(),
                tenant.Settings.Culture,
                tenant.Settings.TimeZone,
                tenant.Settings.ContactName,
                tenant.Settings.ContactEmail,
                tenant.Settings.ContactPhone,
                tenant.CreatedAtUtc,
                tenant.UpdatedAtUtc,
                tenant.ConcurrencyStamp))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AdminPassword != request.AdminConfirmPassword)
        {
            return OperationResult<Guid>.Failure("A confirmação da senha não confere.");
        }

        string slug;
        try
        {
            slug = TenantSlug.Create(request.Slug);
        }
        catch (ArgumentException)
        {
            return OperationResult<Guid>.Failure("Informe um slug válido para a organização.");
        }

        if (await SlugExistsAsync(slug, excludedTenantId: null, cancellationToken))
        {
            return OperationResult<Guid>.Failure("Este slug de organização já está em uso.");
        }

        string normalizedEmail = _userManager.NormalizeEmail(request.AdminEmail);
        bool emailExists = await _userManager.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (emailExists)
        {
            return OperationResult<Guid>.Failure("Este e-mail já está em uso.");
        }

        IDbContextTransaction? transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        Tenant? tenant = null;
        await using (transaction)
        {
            try
            {
                tenant = Tenant.Create(request.Name, slug);
                tenant.Settings.UpdateLocalization(request.Culture, request.TimeZone);
                tenant.Settings.UpdateContact(request.ContactName, request.ContactEmail, request.ContactPhone);
            }
            catch (ArgumentException exception)
            {
                return OperationResult<Guid>.Failure(exception.Message);
            }

            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var admin = new ApplicationUser
            {
                UserName = request.AdminEmail,
                Email = request.AdminEmail,
                EmailConfirmed = true,
                FullName = request.AdminFullName.Trim(),
                TenantId = tenant.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            IdentityResult created = await _userManager.CreateAsync(admin, request.AdminPassword);
            if (!created.Succeeded)
            {
                await CleanupTenantAsync(tenant, transaction, cancellationToken);
                return OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(created.Errors));
            }

            IdentityResult roleAssigned = await _userManager.AddToRoleAsync(admin, OrizonRoles.TenantAdmin);
            if (!roleAssigned.Succeeded)
            {
                await _userManager.DeleteAsync(admin);
                await CleanupTenantAsync(tenant, transaction, cancellationToken);
                return OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(roleAssigned.Errors));
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return OperationResult<Guid>.Success(tenant.Id);
        }
    }

    public async Task<OperationResult> UpdateAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _dbContext.Tenants
            .Include(candidate => candidate.Settings)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return OperationResult.Failure("Organização não encontrada.");
        }

        try
        {
            tenant.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            string slug = TenantSlug.Create(request.Slug);
            if (await SlugExistsAsync(slug, tenant.Id, cancellationToken))
            {
                return OperationResult.Failure("Este slug de organização já está em uso.");
            }

            tenant.Rename(request.Name);
            tenant.ChangeSlug(slug);
            tenant.Settings.UpdateLocalization(request.Culture, request.TimeZone);
            tenant.Settings.UpdateContact(request.ContactName, request.ContactEmail, request.ContactPhone);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("A organização foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> SuspendAsync(
        SuspendTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _dbContext.Tenants.SingleOrDefaultAsync(
            candidate => candidate.Id == request.TenantId,
            cancellationToken);
        if (tenant is null)
        {
            return OperationResult.Failure("Organização não encontrada.");
        }

        try
        {
            tenant.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            tenant.Suspend(request.Reason, DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("A organização foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> ReactivateAsync(
        ReactivateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _dbContext.Tenants.SingleOrDefaultAsync(
            candidate => candidate.Id == request.TenantId,
            cancellationToken);
        if (tenant is null)
        {
            return OperationResult.Failure("Organização não encontrada.");
        }

        try
        {
            tenant.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            tenant.Reactivate(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("A organização foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> UpdateOwnSettingsAsync(
        UpdateOwnTenantSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _dbContext.Tenants
            .Include(candidate => candidate.Settings)
            .SingleOrDefaultAsync(candidate => candidate.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return OperationResult.Failure("Organização não encontrada.");
        }

        try
        {
            tenant.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            tenant.Rename(request.Name);
            tenant.Settings.UpdateLocalization(request.Culture, request.TimeZone);
            tenant.Settings.UpdateContact(request.ContactName, request.ContactEmail, request.ContactPhone);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("A organização foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<bool> IsTenantSuspendedAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId && tenant.Status == TenantStatus.Suspended, cancellationToken);
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludedTenantId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Tenants.AnyAsync(
            tenant => tenant.Slug == slug && (!excludedTenantId.HasValue || tenant.Id != excludedTenantId.Value),
            cancellationToken);
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

    private async Task<RecentUserDto[]> GetRecentUsersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId)
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.FullName)
            .Take(RecentUsersLimit)
            .Select(user => new RecentUserDto(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                null,
                user.IsActive,
                user.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private async Task CleanupTenantAsync(
        Tenant tenant,
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            return;
        }

        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
