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
        await LoadAgentsAsync(cancellationToken);

        return View(new ApiCredentialCreateViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        ApiCredentialCreateViewModel form,
        CancellationToken cancellationToken)
    {
        await LoadAgentsAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        AiAgentDetailsDto? agent =
            await _aiAgentService.GetAsync(
                form.AgentId!.Value,
                cancellationToken);

        if (agent is null)
        {
            ModelState.AddModelError(
                nameof(form.AgentId),
                "O agente selecionado não foi encontrado.");

            return View(form);
        }

        if (!agent.IsActive)
        {
            ModelState.AddModelError(
                nameof(form.AgentId),
                "O agente selecionado está inativo.");

            return View(form);
        }

        CreatedApiCredential credential =
            await _apiCredentialService.CreateAsync(
                GetTenantId(),
                agent.Id,
                form.Name,
                cancellationToken);

        form.Name = string.Empty;
        form.CreatedApiKey = credential.ApiKey;

        await LoadAgentsAsync(cancellationToken);

        return View(form);
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
