using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Email;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Accounts;

public sealed class AccountService : IAccountService
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;

    public AccountService(
        OrizonAgentsDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    public async Task<OperationResult<Guid>> RegisterOrganizationAsync(
        RegisterOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.AcceptedTerms)
        {
            return OperationResult<Guid>.Failure("Você precisa aceitar os termos para continuar.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return OperationResult<Guid>.Failure("A confirmação da senha não confere.");
        }

        string normalizedEmail = _userManager.NormalizeEmail(request.Email);
        bool emailExists = await _userManager.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            return OperationResult<Guid>.Failure("Este e-mail já está em uso.");
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

        bool slugExists = await _dbContext.Tenants.AnyAsync(tenant => tenant.Slug == slug, cancellationToken);
        if (slugExists)
        {
            return OperationResult<Guid>.Failure("Este slug de organização já está em uso.");
        }

        IDbContextTransaction? transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await using (transaction)
        {
            Tenant tenant;
            try
            {
                tenant = Tenant.Create(request.OrganizationName, slug);
            }
            catch (ArgumentException exception)
            {
                return OperationResult<Guid>.Failure(exception.Message);
            }

            _dbContext.Tenants.Add(tenant);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName.Trim(),
                TenantId = tenant.Id,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            IdentityResult created = await _userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                return OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(created.Errors));
            }

            IdentityResult roleAssigned = await _userManager.AddToRoleAsync(user, OrizonRoles.TenantAdmin);
            if (!roleAssigned.Succeeded)
            {
                return OperationResult<Guid>.Failure(IdentityErrorTranslator.Translate(roleAssigned.Errors));
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return OperationResult<Guid>.Success(user.Id);
        }
    }

    public async Task<OperationResult> PasswordSignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return OperationResult.Failure("E-mail ou senha inválidos.");
        }

        if (!user.IsActive)
        {
            return OperationResult.Failure("Sua conta está inativa. Fale com o administrador da organização.");
        }

        bool isPlatformAdmin = await _userManager.IsInRoleAsync(user, OrizonRoles.PlatformAdmin);
        if (!isPlatformAdmin && user.TenantId.HasValue)
        {
            bool tenantSuspended = await _dbContext.Tenants
                .AsNoTracking()
                .AnyAsync(
                    tenant => tenant.Id == user.TenantId.Value && tenant.Status == TenantStatus.Suspended,
                    cancellationToken);

            if (tenantSuspended)
            {
                return OperationResult.Failure("Sua organização está temporariamente suspensa.");
            }
        }

        SignInResult result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            return OperationResult.Success();
        }

        if (result.IsLockedOut)
        {
            return OperationResult.Failure("Muitas tentativas inválidas. Tente novamente mais tarde.");
        }

        return OperationResult.Failure("E-mail ou senha inválidos.");
    }

    public async Task<string> GetPostLoginPathAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return "/inicio";
        }

        if (await _userManager.IsInRoleAsync(user, OrizonRoles.PlatformAdmin))
        {
            return "/Platform/Dashboard";
        }

        if (await _userManager.IsInRoleAsync(user, OrizonRoles.TenantAdmin))
        {
            return "/Admin/Dashboard";
        }

        return "/inicio";
    }

    public Task SignOutAsync()
    {
        return _signInManager.SignOutAsync();
    }

    public async Task<OperationResult> SendPasswordResetAsync(
        ForgotPasswordRequest request,
        Func<string, string, string> resetLinkFactory,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
        {
            return OperationResult.Success();
        }

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        string link = resetLinkFactory(user.Email!, token);
        await _emailSender.SendAccountLinkAsync(user.Email!, "Redefinição de senha", link, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return OperationResult.Failure("A confirmação da senha não confere.");
        }

        ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return OperationResult.Failure("Não foi possível redefinir a senha.");
        }

        IdentityResult result = await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(result.Errors));
    }

    public async Task<OperationResult> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return OperationResult.Failure("Usuário não encontrado.");
        }

        IdentityResult result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(result.Errors));
    }

    public async Task<ProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _userManager.Users
            .Include(user => user.Tenant)
            .Where(user => user.Id == userId)
            .Select(user => new ProfileDto(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                user.Tenant == null ? null : user.Tenant.Name,
                user.Tenant == null ? null : user.Tenant.Slug))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return OperationResult.Failure("Usuário não encontrado.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return OperationResult.Failure("Informe seu nome completo.");
        }

        user.FullName = request.FullName.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;
        IdentityResult result = await _userManager.UpdateAsync(user);

        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(result.Errors));
    }

    public async Task<OperationResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return OperationResult.Failure("A confirmação da senha não confere.");
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return OperationResult.Failure("Usuário não encontrado.");
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        return result.Succeeded
            ? OperationResult.Success()
            : OperationResult.Failure(IdentityErrorTranslator.Translate(result.Errors));
    }
}
