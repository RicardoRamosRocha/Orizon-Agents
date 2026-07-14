using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;
using OrizonAgents.Web.Models.WhatsApp;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("Admin/WhatsApp")]
public sealed class WhatsAppController : Controller
{
    private readonly IWhatsAppConnectionService _connections;
    private readonly IWhatsAppTemplateService _templates;
    private readonly IWhatsAppMessagingService _messages;

    public WhatsAppController(IWhatsAppConnectionService connections, IWhatsAppTemplateService templates, IWhatsAppMessagingService messages)
    {
        _connections = connections;
        _templates = templates;
        _messages = messages;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        return View(new WhatsAppIndexViewModel { Summary = await _connections.GetTenantSummaryAsync(tenantId, cancellationToken) });
    }

    [HttpPost("conexoes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WhatsAppConnectionFormViewModel form, CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        if (!ModelState.IsValid) return View("Index", new WhatsAppIndexViewModel { Summary = await _connections.GetTenantSummaryAsync(tenantId, cancellationToken), Form = form });
        OperationResult<Guid> result = await _connections.CreateConnectionAsync(new CreateWhatsAppConnectionRequest(tenantId, form.Name, form.WhatsAppBusinessAccountId, form.PhoneNumberId, form.DisplayPhoneNumber, form.VerifiedName, form.AccessToken, form.IsDefault), cancellationToken);
        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View("Index", new WhatsAppIndexViewModel { Summary = await _connections.GetTenantSummaryAsync(tenantId, cancellationToken), Form = form });
        }

        TempData["StatusMessage"] = "Conexão criada com segurança.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/validar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validate(Guid id, CancellationToken cancellationToken)
    {
        await _connections.ValidateConnectionAsync(GetTenantId(), id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/padrao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _connections.SetDefaultAsync(GetTenantId(), id, concurrencyStamp, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/desconectar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _connections.DisconnectAsync(GetTenantId(), id, concurrencyStamp, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceToken(Guid id, string accessToken, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _connections.ReplaceTokenAsync(GetTenantId(), id, new ReplaceWhatsAppTokenRequest(accessToken, concurrencyStamp), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Templates")]
    public async Task<IActionResult> Templates([FromQuery] WhatsAppTemplatesViewModel model, CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        model.Result = await _templates.ListTemplatesAsync(new WhatsAppTemplateListRequest(tenantId, model.ConnectionId, model.Status, model.PageNumber), cancellationToken);
        model.Connections = (await _connections.GetTenantSummaryAsync(tenantId, cancellationToken)).Connections;
        return View(model);
    }

    [HttpPost("Templates/sincronizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncTemplates(Guid connectionId, CancellationToken cancellationToken)
    {
        await _templates.SynchronizeTemplatesAsync(GetTenantId(), connectionId, cancellationToken);
        return RedirectToAction(nameof(Templates));
    }

    [HttpGet("Messages")]
    public async Task<IActionResult> Messages([FromQuery] WhatsAppMessagesViewModel model, CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        WhatsAppPagedMessagesDto dto = await _messages.ListMessagesAsync(new WhatsAppMessageListRequest(tenantId, model.Direction, model.Status, model.PageNumber), cancellationToken);
        model.Result = dto.Messages;
        model.Usage = dto.Usage;
        return View(model);
    }

    private Guid GetTenantId()
        => Guid.TryParse(User.FindFirst(OrizonClaimTypes.TenantId)?.Value, out Guid tenantId) ? tenantId : throw new UnauthorizedAccessException();

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors) ModelState.AddModelError(string.Empty, error);
    }
}
