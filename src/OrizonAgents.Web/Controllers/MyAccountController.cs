using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Web.Models.Account;

namespace OrizonAgents.Web.Controllers;

[Authorize]
[Route("minha-conta")]
public sealed class MyAccountController : Controller
{
    private readonly IAccountService _accountService;

    public MyAccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        Guid userId = GetUserId();
        ProfileDto? profile = await _accountService.GetProfileAsync(userId);
        if (profile is null)
        {
            return NotFound();
        }

        return View(new EditProfileViewModel
        {
            FullName = profile.FullName,
            Email = profile.Email,
            TenantName = profile.TenantName,
            TenantSlug = profile.TenantSlug
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _accountService.UpdateProfileAsync(
            new UpdateProfileRequest(GetUserId(), model.FullName));

        TempData["StatusMessage"] = result.Succeeded
            ? "Perfil atualizado com sucesso."
            : result.FirstError ?? "Não foi possível atualizar o perfil.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("senha")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost("senha")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _accountService.ChangePasswordAsync(
            new ChangePasswordRequest(
                GetUserId(),
                model.CurrentPassword,
                model.NewPassword,
                model.ConfirmPassword));

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        TempData["StatusMessage"] = "Senha alterada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("encerrar-outras-sessoes")]
    [ValidateAntiForgeryToken]
    public IActionResult RevokeOtherSessions()
    {
        TempData["StatusMessage"] = "Encerramento de outras sessões será habilitado quando houver infraestrutura de sessões persistentes.";
        return RedirectToAction(nameof(Index));
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
