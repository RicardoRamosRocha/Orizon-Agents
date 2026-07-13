using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Infrastructure.Identity;

namespace OrizonAgents.Infrastructure.Accounts;

public sealed class TenantUserService : ITenantUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantUserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyCollection<UserAccountDto>> SearchAsync(
        Guid tenantId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ApplicationUser> query = _userManager.Users
            .Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalized = search.Trim().ToUpperInvariant();
            query = query.Where(user =>
                user.FullName.ToUpper().Contains(normalized)
                || (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized)));
        }

        ApplicationUser[] users = await query
            .OrderBy(user => user.FullName)
            .ToArrayAsync(cancellationToken);

        var result = new List<UserAccountDto>(users.Length);
        foreach (ApplicationUser user in users)
        {
            result.Add(await ToDtoAsync(user));
        }

        return result;
    }

    public async Task<UserAccountDto?> GetAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.TenantId == tenantId,
            cancellationToken);

        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return OperationResult<Guid>.Failure("A confirmação da senha não confere.");
        }

        if (!IsTenantRole(request.Role))
        {
            return OperationResult<Guid>.Failure("Papel inválido para usuários da organização.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            TenantId = request.TenantId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(created.Errors));
        }

        IdentityResult roleAssigned = await _userManager.AddToRoleAsync(user, request.Role);
        return roleAssigned.Succeeded
            ? OperationResult<Guid>.Success(user.Id)
            : OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(roleAssigned.Errors));
    }

    public async Task<OperationResult> UpdateAsync(
        UpdateTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsTenantRole(request.Role))
        {
            return OperationResult.Failure("Papel inválido para usuários da organização.");
        }

        ApplicationUser? user = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == request.UserId && candidate.TenantId == request.TenantId,
            cancellationToken);

        if (user is null)
        {
            return OperationResult.Failure("Usuário não encontrado nesta organização.");
        }

        if (request.UserId == request.ActingUserId && !request.IsActive)
        {
            return OperationResult.Failure("Você não pode desativar sua própria conta.");
        }

        bool isTenantAdmin = await _userManager.IsInRoleAsync(user, OrizonRoles.TenantAdmin);
        if (isTenantAdmin && (!request.IsActive || request.Role != OrizonRoles.TenantAdmin))
        {
            int activeAdmins = await CountActiveTenantAdminsAsync(request.TenantId, cancellationToken);
            if (activeAdmins <= 1)
            {
                return OperationResult.Failure("A organização deve manter ao menos um TenantAdmin ativo.");
            }
        }

        user.FullName = request.FullName.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        IdentityResult updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return OperationResult.Failure(IdentityErrorTranslator.Translate(updated.Errors));
        }

        IList<string> roles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, roles.Where(IsTenantRole));
        IdentityResult roleAssigned = await _userManager.AddToRoleAsync(user, request.Role);

        return roleAssigned.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(roleAssigned.Errors));
    }

    public async Task<OperationResult> ResetPasswordAsync(
        Guid tenantId,
        Guid userId,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (newPassword != confirmPassword)
        {
            return OperationResult.Failure("A confirmação da senha não confere.");
        }

        ApplicationUser? user = await _userManager.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == userId && candidate.TenantId == tenantId,
            cancellationToken);

        if (user is null)
        {
            return OperationResult.Failure("Usuário não encontrado nesta organização.");
        }

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(result.Errors));
    }

    private static bool IsTenantRole(string role)
    {
        return role is OrizonRoles.TenantAdmin or OrizonRoles.TenantMember;
    }

    private async Task<int> CountActiveTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        ApplicationUser[] activeUsers = await _userManager.Users
            .Where(user => user.TenantId == tenantId && user.IsActive)
            .ToArrayAsync(cancellationToken);

        int count = 0;
        foreach (ApplicationUser user in activeUsers)
        {
            if (await _userManager.IsInRoleAsync(user, OrizonRoles.TenantAdmin))
            {
                count++;
            }
        }

        return count;
    }

    private async Task<UserAccountDto> ToDtoAsync(ApplicationUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        return new UserAccountDto(
            user.Id,
            user.TenantId,
            user.FullName,
            user.Email ?? string.Empty,
            user.IsActive,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            roles.ToArray());
    }
}
