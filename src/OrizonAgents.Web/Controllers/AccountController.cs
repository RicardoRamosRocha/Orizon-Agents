using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Web.Models.Account;

namespace OrizonAgents.Web.Controllers;

[Route("conta")]
public sealed class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("entrar")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("entrar")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _accountService.PasswordSignInAsync(
            new LoginRequest(model.Email, model.Password, model.RememberMe));

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.FirstError ?? "Não foi possível entrar.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        string path = await _accountService.GetPostLoginPathAsync(model.Email);
        return LocalRedirect(path);
    }

    [HttpGet("nova-organizacao")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterOrganizationViewModel());
    }

    [HttpPost("nova-organizacao")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterOrganizationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult<Guid> result = await _accountService.RegisterOrganizationAsync(
            new RegisterOrganizationRequest(
                model.OrganizationName,
                model.Slug,
                model.FullName,
                model.Email,
                model.Password,
                model.ConfirmPassword,
                model.AcceptedTerms));

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        TempData["StatusMessage"] = "Organização criada com sucesso. Entre com seu e-mail e senha.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("esqueci-minha-senha")]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("esqueci-minha-senha")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _accountService.SendPasswordResetAsync(
            new ForgotPasswordRequest(model.Email),
            (email, token) => Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email, token = Uri.EscapeDataString(token) },
                Request.Scheme) ?? string.Empty);

        return View("ForgotPasswordConfirmation");
    }

    [HttpGet("redefinir-senha")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email, string token)
    {
        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = Uri.UnescapeDataString(token)
        });
    }

    [HttpPost("redefinir-senha")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _accountService.ResetPasswordAsync(
            new ResetPasswordRequest(model.Email, model.Token, model.Password, model.ConfirmPassword));

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        return View("ResetPasswordConfirmation");
    }

    [HttpGet("confirmar-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
    {
        OperationResult result = await _accountService.ConfirmEmailAsync(userId, Uri.UnescapeDataString(token));
        ViewData["Succeeded"] = result.Succeeded;
        ViewData["Message"] = result.Succeeded
            ? "E-mail confirmado com sucesso."
            : result.FirstError ?? "Não foi possível confirmar o e-mail.";

        return View();
    }

    [HttpGet("acesso-negado")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet("organizacao-suspensa")]
    [Authorize]
    public IActionResult SuspendedOrganization()
    {
        return View();
    }

    [HttpPost("sair")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _accountService.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
