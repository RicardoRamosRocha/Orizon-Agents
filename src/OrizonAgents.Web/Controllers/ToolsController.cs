using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Web.Models.Tools;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("tools")]
public sealed class ToolsController : Controller
{
    private readonly IAgentToolService _toolService;
    private readonly IToolCredentialService _credentialService;
    private readonly IIntegrationConnectionService _connectionService;
    private readonly IGoogleOAuthCapabilityService _capabilities;
    private readonly ILogger<ToolsController> _logger;

    public ToolsController(
        IAgentToolService toolService,
        IToolCredentialService credentialService,
        IIntegrationConnectionService connectionService,
        IGoogleOAuthCapabilityService capabilities,
        ILogger<ToolsController> logger)
    {
        _toolService = toolService;
        _credentialService = credentialService;
        _connectionService = connectionService;
        _capabilities = capabilities;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentToolListItemDto> tools = await _toolService.ListAsync(cancellationToken);
        return View(tools);
    }

    [HttpGet("nova")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var form = new AgentToolFormViewModel();
        await PopulateOptionsAsync(form, cancellationToken);
        return View(form);
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AgentToolFormViewModel form,
        CancellationToken cancellationToken)
    {
        ValidateCreateForm(form);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(form, cancellationToken);
            return View(form);
        }

        bool isGmail = form.Category == AgentToolCategory.Gmail;
        AgentToolKind kind = isGmail
            ? MapGmailAction(form.GmailAction)
            : AgentToolKind.Http;

        OperationResult<Guid> result = await _toolService.CreateAsync(
            new CreateAgentToolRequest(
                GetTenantId(),
                form.Name,
                form.Description,
                form.Endpoint,
                form.HttpMethod,
                form.InputSchema,
                form.ToolCredentialId,
                form.RiskLevel,
                kind,
                isGmail ? form.IntegrationConnectionId : null),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateOptionsAsync(form, cancellationToken);
            return View(form);
        }

        TempData["StatusMessage"] = "Tool criada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        AgentToolDetailsDto? tool = await _toolService.GetAsync(id, cancellationToken);
        if (tool is null)
        {
            return NotFound();
        }

        var form = new AgentToolFormViewModel
        {
            Id = tool.Id,
            Name = tool.Name,
            Description = tool.Description,
            Endpoint = tool.Endpoint,
            HttpMethod = tool.HttpMethod,
            RiskLevel = tool.RiskLevel,
            InputSchema = tool.InputSchema,
            ToolCredentialId = tool.ToolCredentialId,
            IsActive = tool.IsActive
        };
        ApplyToolType(form, tool);
        await PopulateOptionsAsync(form, cancellationToken);
        return View(form);
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        AgentToolFormViewModel form,
        CancellationToken cancellationToken)
    {
        form.Id = id;
        AgentToolDetailsDto? tool = await _toolService.GetAsync(id, cancellationToken);
        if (tool is null)
        {
            return NotFound();
        }
        ApplyToolType(form, tool);
        ValidateEditForm(form, tool.Kind);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(form, cancellationToken);
            return View(form);
        }

        OperationResult result = await _toolService.UpdateAsync(
            new UpdateAgentToolRequest(
                id,
                form.Name,
                form.Description,
                form.Endpoint,
                form.HttpMethod,
                form.InputSchema,
                form.ToolCredentialId,
                form.RiskLevel,
                tool.Kind == AgentToolKind.Http ? null : form.IntegrationConnectionId),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            await PopulateOptionsAsync(form, cancellationToken);
            return View(form);
        }

        TempData["StatusMessage"] = "Tool atualizada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        bool activate,
        CancellationToken cancellationToken)
    {
        OperationResult result = activate
            ? await _toolService.ActivateAsync(id, cancellationToken)
            : await _toolService.DeactivateAsync(id, cancellationToken);

        TempData["StatusMessage"] = result.Succeeded
            ? activate ? "Tool ativada." : "Tool desativada."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateOptionsAsync(
        AgentToolFormViewModel form,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ToolCredentialListItemDto> credentials =
            await _credentialService.ListAsync(cancellationToken);
        form.CredentialOptions = credentials
            .Select(x => new SelectListItem(
                x.IsActive ? $"{x.Name} ({x.AuthenticationType})" : $"{x.Name} ({x.AuthenticationType}, inativa)",
                x.Id.ToString(),
                x.Id == form.ToolCredentialId))
            .ToArray();

        var eligibleConnections = new List<SelectListItem>();
        IReadOnlyList<IntegrationConnectionDto> connections =
            await _connectionService.ListAsync(cancellationToken);
        foreach (IntegrationConnectionDto connection in connections.Where(connection =>
                     connection.Provider == IntegrationProvider.Gmail &&
                     connection.IsActive &&
                     connection.Status == IntegrationConnectionStatus.Connected))
        {
            try
            {
                if (!await _capabilities.HasCapabilityAsync(
                        connection.Id, GoogleOAuthCapability.GmailRead, cancellationToken))
                {
                    continue;
                }
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Falha ao consultar autorização Gmail para formulário de Tool. Tipo: {ExceptionType}.",
                    exception.GetType().Name);
                continue;
            }

            string label = string.IsNullOrWhiteSpace(connection.ConnectedAccountEmail)
                ? connection.Name
                : $"{connection.Name} ({connection.ConnectedAccountEmail})";
            eligibleConnections.Add(new SelectListItem(
                label,
                connection.Id.ToString(),
                connection.Id == form.IntegrationConnectionId));
        }
        form.GmailConnectionOptions = eligibleConnections;
    }

    private void ValidateCreateForm(AgentToolFormViewModel form)
    {
        if (!Enum.IsDefined(form.Category))
        {
            ModelState.AddModelError(nameof(form.Category), "Selecione um tipo de ferramenta válido.");
            return;
        }

        if (form.Category == AgentToolCategory.Http)
        {
            ValidateHttpFields(form);
            return;
        }

        if (!Enum.IsDefined(form.GmailAction))
        {
            ModelState.AddModelError(nameof(form.GmailAction), "Selecione uma ação Gmail válida.");
        }
        if (!form.IntegrationConnectionId.HasValue || form.IntegrationConnectionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(form.IntegrationConnectionId), "Selecione uma conexão Google autorizada.");
        }
    }

    private void ValidateEditForm(AgentToolFormViewModel form, AgentToolKind kind)
    {
        if (kind == AgentToolKind.Http)
        {
            ValidateHttpFields(form);
        }
        else if (!form.IntegrationConnectionId.HasValue || form.IntegrationConnectionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(form.IntegrationConnectionId), "Selecione uma conexão Google autorizada.");
        }
    }

    private void ValidateHttpFields(AgentToolFormViewModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Endpoint))
        {
            ModelState.AddModelError(nameof(form.Endpoint), "Informe o endpoint.");
        }
        if (string.IsNullOrWhiteSpace(form.HttpMethod))
        {
            ModelState.AddModelError(nameof(form.HttpMethod), "Informe o método HTTP.");
        }
    }

    private static AgentToolKind MapGmailAction(GmailToolAction action) => action switch
    {
        GmailToolAction.SearchEmails => AgentToolKind.GmailSearch,
        GmailToolAction.ReadEmail => AgentToolKind.GmailReadMessage,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static void ApplyToolType(AgentToolFormViewModel form, AgentToolDetailsDto tool)
    {
        form.IsEdit = true;
        form.Category = tool.Kind == AgentToolKind.Http ? AgentToolCategory.Http : AgentToolCategory.Gmail;
        form.GmailAction = tool.Kind == AgentToolKind.GmailReadMessage
            ? GmailToolAction.ReadEmail
            : GmailToolAction.SearchEmails;
        form.IntegrationConnectionId = tool.IntegrationConnectionId;
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
