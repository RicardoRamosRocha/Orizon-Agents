using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Models;
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
    private readonly IAiAgentService _aiAgentService;

    public IntegrationsController(
        IApiCredentialService apiCredentialService,
        IAiAgentService aiAgentService)
    {
        _apiCredentialService = apiCredentialService;
        _aiAgentService = aiAgentService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        IntegrationsIndexViewModel model =
            await BuildViewModelAsync(
                new ApiCredentialCreateViewModel(),
                cancellationToken);

        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        IntegrationsIndexViewModel model,
        CancellationToken cancellationToken)
    {
        await LoadAgentsAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            model.Credentials =
                await _apiCredentialService.ListAsync(
                    GetTenantId(),
                    cancellationToken);

            return View(model);
        }

        AiAgentDetailsDto? agent =
            await _aiAgentService.GetAsync(
                model.Create.AgentId!.Value,
                cancellationToken);

        if (agent is null)
        {
            ModelState.AddModelError(
                "Create.AgentId",
                "O agente selecionado não foi encontrado.");

            model.Credentials =
                await _apiCredentialService.ListAsync(
                    GetTenantId(),
                    cancellationToken);

            return View(model);
        }

        if (!agent.IsActive)
        {
            ModelState.AddModelError(
                "Create.AgentId",
                "O agente selecionado está inativo.");

            model.Credentials =
                await _apiCredentialService.ListAsync(
                    GetTenantId(),
                    cancellationToken);

            return View(model);
        }

        CreatedApiCredential credential =
            await _apiCredentialService.CreateAsync(
                GetTenantId(),
                agent.Id,
                model.Create.Name,
                cancellationToken);

        model = await BuildViewModelAsync(
            new ApiCredentialCreateViewModel
            {
                CreatedApiKey = credential.ApiKey
            },
            cancellationToken);

        return View(model);
    }

    [HttpPost("revogar/{credentialId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _apiCredentialService.RevokeAsync(
                GetTenantId(),
                credentialId,
                cancellationToken);

            TempData["IntegrationSuccess"] =
                "API Key revogada com sucesso.";
        }
        catch (InvalidOperationException)
        {
            TempData["IntegrationError"] =
                "Não foi possível revogar a API Key informada.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("regenerar/{credentialId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regenerate(
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        try
        {
            CreatedApiCredential credential =
                await _apiCredentialService.RegenerateAsync(
                    GetTenantId(),
                    credentialId,
                    cancellationToken);

            IntegrationsIndexViewModel model =
                await BuildViewModelAsync(
                    new ApiCredentialCreateViewModel
                    {
                        CreatedApiKey = credential.ApiKey
                    },
                    cancellationToken);

            ViewData["IntegrationSuccess"] =
                "API Key regenerada. Copie a nova chave agora.";

            return View("Index", model);
        }
        catch (InvalidOperationException)
        {
            TempData["IntegrationError"] =
                "Não foi possível regenerar a API Key informada.";

            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<IntegrationsIndexViewModel> BuildViewModelAsync(
        ApiCredentialCreateViewModel create,
        CancellationToken cancellationToken)
    {
        await LoadAgentsAsync(cancellationToken);

        IReadOnlyList<ApiCredentialListItem> credentials =
            await _apiCredentialService.ListAsync(
                GetTenantId(),
                cancellationToken);

        return new IntegrationsIndexViewModel
        {
            Create = create,
            Credentials = credentials
        };
    }

    private async Task LoadAgentsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AiAgentListItemDto> agents =
            await _aiAgentService.ListAsync(cancellationToken);

        ViewBag.Agents = agents
            .Where(agent => agent.IsActive)
            .Select(agent => new SelectListItem
            {
                Value = agent.Id.ToString(),
                Text = agent.Name
            })
            .ToList();
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
