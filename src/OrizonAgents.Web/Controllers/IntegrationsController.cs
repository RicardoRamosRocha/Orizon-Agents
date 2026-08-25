using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Web.Models.Integrations;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("integracoes")]
public sealed class IntegrationsController : Controller
{
    private readonly IApiCredentialService _apiCredentialService;

    public IntegrationsController(
        IApiCredentialService apiCredentialService)
    {
        _apiCredentialService = apiCredentialService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new ApiCredentialCreateViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        ApiCredentialCreateViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        CreatedApiCredential credential =
            await _apiCredentialService.CreateAsync(
                GetTenantId(),
                form.Name,
                cancellationToken);

        form.Name = string.Empty;
        form.CreatedApiKey = credential.ApiKey;

        return View(form);
    }

    private Guid GetTenantId()
    {
        string? value =
            User.FindFirstValue(OrizonClaimTypes.TenantId);

        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException(
                "Usuário autenticado sem tenant.");
    }
}
