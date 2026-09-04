using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Requests;
using OrizonAgents.Web.Models.Integrations;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("integracoes/conexoes")]
public sealed class ConnectionsController(IIntegrationConnectionService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildPageAsync(new ConnectionCreateViewModel(), cancellationToken));

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] ConnectionCreateViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(form, cancellationToken));
        }

        OperationResult<Guid> result = await service.CreateAsync(
            new CreateIntegrationConnectionRequest(form.Name, form.Provider), cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View("Index", await BuildPageAsync(form, cancellationToken));
        }

        TempData["StatusMessage"] = "Conexão criada. Use Conectar com Google para autorizar a conta.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var connection = await service.GetAsync(id, cancellationToken);
        return connection is null
            ? NotFound()
            : View(new ConnectionDetailsViewModel
            {
                Connection = connection,
                Edit = new ConnectionEditViewModel { Name = connection.Name }
            });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        [Bind(Prefix = "Edit")] ConnectionEditViewModel form,
        CancellationToken cancellationToken)
    {
        var connection = await service.GetAsync(id, cancellationToken);
        if (connection is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            OperationResult result = await service.UpdateAsync(
                id, new UpdateIntegrationConnectionRequest(form.Name), cancellationToken);
            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Conexão atualizada.";
                return RedirectToAction(nameof(Details), new { id });
            }

            AddErrors(result.Errors);
        }

        return View("Details", new ConnectionDetailsViewModel { Connection = connection, Edit = form });
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(Guid id, bool activate, CancellationToken cancellationToken)
    {
        OperationResult result = await service.SetActiveAsync(id, activate, cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? activate ? "Conexão ativada." : "Conexão desativada."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/remover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        OperationResult result = await service.DeleteAsync(id, cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? "Conexão removida."
            : string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    private async Task<ConnectionsPageViewModel> BuildPageAsync(
        ConnectionCreateViewModel form, CancellationToken cancellationToken) =>
        new() { Create = form, Connections = await service.ListAsync(cancellationToken) };

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}