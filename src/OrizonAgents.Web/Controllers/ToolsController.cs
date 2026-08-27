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
[Route("tools")]
public sealed class ToolsController : Controller
{
    private readonly IAgentToolService _toolService;

    public ToolsController(IAgentToolService toolService)
    {
        _toolService = toolService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AgentToolListItemDto> tools =
            await _toolService.ListAsync(cancellationToken);

        return View(tools);
    }

    [HttpGet("nova")]
    public IActionResult Create()
    {
        return View(new AgentToolFormViewModel());
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AgentToolFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        OperationResult<Guid> result =
            await _toolService.CreateAsync(
                new CreateAgentToolRequest(
                    GetTenantId(),
                    form.Name,
                    form.Description,
                    form.Endpoint,
                    form.HttpMethod,
                    form.InputSchema),
                cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(form);
        }

        TempData["StatusMessage"] =
            "Tool criada com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken)
    {
        AgentToolDetailsDto? tool =
            await _toolService.GetAsync(id, cancellationToken);

        if (tool is null)
        {
            return NotFound();
        }

        return View(new AgentToolFormViewModel
        {
            Id = tool.Id,
            Name = tool.Name,
            Description = tool.Description,
            Endpoint = tool.Endpoint,
            HttpMethod = tool.HttpMethod,
            InputSchema = tool.InputSchema,
            IsActive = tool.IsActive
        });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        AgentToolFormViewModel form,
        CancellationToken cancellationToken)
    {
        form.Id = id;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        OperationResult result =
            await _toolService.UpdateAsync(
                new UpdateAgentToolRequest(
                    id,
                    form.Name,
                    form.Description,
                    form.Endpoint,
                    form.HttpMethod,
                    form.InputSchema),
                cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(form);
        }

        TempData["StatusMessage"] =
            "Tool atualizada com sucesso.";

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
            ? activate
                ? "Tool ativada."
                : "Tool desativada."
            : string.Join(" ", result.Errors);

        return RedirectToAction(nameof(Index));
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

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error);
        }
    }
}
