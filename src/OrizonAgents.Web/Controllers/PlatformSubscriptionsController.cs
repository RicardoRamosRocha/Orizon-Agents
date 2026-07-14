using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Billing.Requests;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Web.Models.Billing;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("Platform/Subscriptions")]
public sealed class PlatformSubscriptionsController : Controller
{
    private readonly IBillingService _billingService;

    public PlatformSubscriptionsController(IBillingService billingService) => _billingService = billingService;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] SubscriptionIndexViewModel model, CancellationToken cancellationToken)
    {
        model.Result = await _billingService.ListSubscriptionsAsync(new SubscriptionListRequest(model.Search, model.PlanId, model.Status, model.PageNumber), cancellationToken);
        model.Plans = await _billingService.GetPublicPlansAsync(cancellationToken);
        return View(model);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        SubscriptionDetailsDto? subscription = await _billingService.GetSubscriptionAsync(id, cancellationToken);
        if (subscription is null) return NotFound();
        ViewData["History"] = await _billingService.GetHistoryAsync(id, cancellationToken);
        ViewData["Plans"] = await _billingService.GetPublicPlansAsync(cancellationToken);
        return View(subscription);
    }

    [HttpPost("{id:guid}/trocar-plano")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(Guid id, Guid planId, string billingCycle, string concurrencyStamp, CancellationToken cancellationToken)
    {
        Enum.TryParse(billingCycle, out Domain.Billing.BillingCycle cycle);
        await _billingService.ChangePlanAsync(new ChangeSubscriptionPlanRequest(id, planId, cycle, concurrencyStamp, GetUserId()), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.CancelImmediatelyAsync(new SubscriptionActionRequest(id, concurrencyStamp, GetUserId()), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/agendar-cancelamento")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleCancel(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.ScheduleCancellationAsync(new SubscriptionActionRequest(id, concurrencyStamp, GetUserId()), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/suspender")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.SuspendAsync(new SubscriptionActionRequest(id, concurrencyStamp, GetUserId()), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        await _billingService.ReactivateAsync(new SubscriptionActionRequest(id, concurrencyStamp, GetUserId()), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private Guid? GetUserId()
    {
        return Guid.TryParse(User.FindFirst(OrizonClaimTypes.UserId)?.Value, out Guid userId) ? userId : null;
    }
}
