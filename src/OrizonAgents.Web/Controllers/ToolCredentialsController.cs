using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Web.Models.Tools;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("tools/credenciais")]
public sealed class ToolCredentialsController : Controller
{
    private readonly IToolCredentialService _credentialService;

    public ToolCredentialsController(IToolCredentialService credentialService)
    {
        _credentialService = credentialService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildPageAsync(new ToolCredentialCreateViewModel(), cancellationToken));

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] ToolCredentialCreateViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(form, cancellationToken));
        }

        OperationResult<Guid> result = await _credentialService.CreateAsync(
            new CreateToolCredentialRequest(
                GetTenantId(),
                form.Name,
                form.AuthenticationType,
                form.HeaderName,
                form.Secret),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View("Index", await BuildPageAsync(form, cancellationToken));
        }

        TempData["StatusMessage"] = "Credencial criada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/secret")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rotate(
        Guid id,
        string secret,
        CancellationToken cancellationToken)
    {
        OperationResult result = await _credentialService.RotateSecretAsync(id, secret, cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? "Secret substituído com sucesso."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        bool activate,
        CancellationToken cancellationToken)
    {
        OperationResult result = await _credentialService.SetActiveAsync(id, activate, cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? activate ? "Credencial ativada." : "Credencial desativada."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    private async Task<ToolCredentialsPageViewModel> BuildPageAsync(
        ToolCredentialCreateViewModel form,
        CancellationToken cancellationToken)
    {
        form.Secret = string.Empty;
        IReadOnlyList<ToolCredentialListItemDto> credentials =
            await _credentialService.ListAsync(cancellationToken);
        return new ToolCredentialsPageViewModel { Create = form, Credentials = credentials };
    }

    private Guid GetTenantId()
    {
        string? value = User.FindFirstValue(OrizonClaimTypes.TenantId);
        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException("Usuário autenticado sem tenant.");
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
