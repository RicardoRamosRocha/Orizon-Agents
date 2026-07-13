using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Web.Models.TenantUsers;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("organizacao/usuarios")]
public sealed class TenantUsersController : Controller
{
    private readonly ITenantUserService _tenantUserService;

    public TenantUsersController(ITenantUserService tenantUserService)
    {
        _tenantUserService = tenantUserService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search)
    {
        IReadOnlyCollection<UserAccountDto> users = await _tenantUserService.SearchAsync(GetTenantId(), search);
        ViewData["Search"] = search;
        return View(users);
    }

    [HttpGet("novo")]
    public IActionResult Create()
    {
        return View(new TenantUserFormViewModel());
    }

    [HttpPost("novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TenantUserFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult<Guid> result = await _tenantUserService.CreateAsync(
            new CreateTenantUserRequest(
                GetTenantId(),
                model.FullName,
                model.Email,
                model.Password,
                model.ConfirmPassword,
                model.Role));

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["StatusMessage"] = "Usuário criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id)
    {
        UserAccountDto? user = await _tenantUserService.GetAsync(GetTenantId(), id);
        if (user is null)
        {
            return NotFound();
        }

        return View(new TenantUserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Roles.Contains(OrizonRoles.TenantAdmin) ? OrizonRoles.TenantAdmin : OrizonRoles.TenantMember,
            IsActive = user.IsActive
        });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TenantUserFormViewModel model)
    {
        model.Id = id;
        ModelState.Remove(nameof(TenantUserFormViewModel.Password));
        ModelState.Remove(nameof(TenantUserFormViewModel.ConfirmPassword));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _tenantUserService.UpdateAsync(
            new UpdateTenantUserRequest(
                GetTenantId(),
                id,
                model.FullName,
                model.Role,
                model.IsActive,
                GetUserId()));

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["StatusMessage"] = "Usuário atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/senha")]
    public IActionResult ResetPassword(Guid id)
    {
        return View(new TenantUserFormViewModel { Id = id });
    }

    [HttpPost("{id:guid}/senha")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, TenantUserFormViewModel model)
    {
        OperationResult result = await _tenantUserService.ResetPasswordAsync(
            GetTenantId(),
            id,
            model.Password,
            model.ConfirmPassword);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            model.Id = id;
            return View(model);
        }

        TempData["StatusMessage"] = "Senha redefinida com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    private Guid GetTenantId()
    {
        string? value = User.FindFirstValue(OrizonClaimTypes.TenantId);
        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException("Usuário autenticado sem tenant.");
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
