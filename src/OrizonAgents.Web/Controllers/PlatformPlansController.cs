using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Billing.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Web.Models.Billing;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("Platform/Plans")]
public sealed class PlatformPlansController : Controller
{
    private readonly IBillingService _billingService;

    public PlatformPlansController(IBillingService billingService) => _billingService = billingService;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] PlanIndexViewModel model, CancellationToken cancellationToken)
    {
        model.Result = await _billingService.ListPlansAsync(new PlanListRequest(model.Search, model.IsActive, model.IsPublic, model.PageNumber), cancellationToken);
        return View(model);
    }

    [HttpGet("criar")]
    public IActionResult Create() => View(BuildForm());

    [HttpPost("criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlanFormViewModel model, CancellationToken cancellationToken)
    {
        EnsureEntitlementKeys(model);
        if (!ModelState.IsValid) return View(model);
        OperationResult<Guid> result = await _billingService.CreatePlanAsync(ToRequest(model), cancellationToken);
        if (!result.Succeeded) { AddErrors(result.Errors); return View(model); }
        TempData["StatusMessage"] = "Plano criado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        PlanDetailsDto? plan = await _billingService.GetPlanAsync(id, cancellationToken);
        return plan is null ? NotFound() : View(plan);
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        PlanDetailsDto? plan = await _billingService.GetPlanAsync(id, cancellationToken);
        return plan is null ? NotFound() : View(ToForm(plan));
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PlanFormViewModel model, CancellationToken cancellationToken)
    {
        EnsureEntitlementKeys(model);
        if (!ModelState.IsValid) { model.Id = id; return View(model); }
        OperationResult result = await _billingService.UpdatePlanAsync(id, ToRequest(model), cancellationToken);
        if (!result.Succeeded) { AddErrors(result.Errors); model.Id = id; return View(model); }
        TempData["StatusMessage"] = "Plano atualizado com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/ativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.SetPlanActiveStateAsync(id, true, concurrencyStamp, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/desativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.SetPlanActiveStateAsync(id, false, concurrencyStamp, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/arquivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.SetPlanArchiveStateAsync(id, true, concurrencyStamp, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private static PlanFormViewModel BuildForm()
    {
        var model = new PlanFormViewModel();
        EnsureEntitlementKeys(model);
        return model;
    }

    private static void EnsureEntitlementKeys(PlanFormViewModel model)
    {
        foreach (string key in PlanFeatureKeys.All)
        {
            model.Entitlements.TryAdd(key, new EntitlementInputViewModel());
        }
    }

    private static PlanFormViewModel ToForm(PlanDetailsDto plan)
    {
        var model = new PlanFormViewModel
        {
            Id = plan.Id,
            Name = plan.Name,
            Code = plan.Code,
            Description = plan.Description,
            MonthlyPrice = plan.MonthlyPrice,
            YearlyPrice = plan.YearlyPrice,
            Currency = plan.Currency,
            TrialDays = plan.TrialDays,
            IsPublic = plan.IsPublic,
            SortOrder = plan.SortOrder,
            ConcurrencyStamp = plan.ConcurrencyStamp,
            Entitlements = plan.Entitlements.ToDictionary(item => item.FeatureKey, item => new EntitlementInputViewModel { IsEnabled = item.IsEnabled, LimitValue = item.LimitValue })
        };
        EnsureEntitlementKeys(model);
        return model;
    }

    private static PlanUpsertRequest ToRequest(PlanFormViewModel model)
    {
        return new PlanUpsertRequest(model.Name, model.Code, model.Description, model.MonthlyPrice, model.YearlyPrice, model.Currency, model.TrialDays, model.IsPublic, model.SortOrder, model.ConcurrencyStamp, model.Entitlements.Select(item => new PlanEntitlementRequest(item.Key, item.Value.IsEnabled, item.Value.LimitValue)).ToArray());
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors) ModelState.AddModelError(string.Empty, error);
    }
}
