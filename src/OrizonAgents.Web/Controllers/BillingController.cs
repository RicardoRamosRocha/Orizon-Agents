using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Web.Controllers;

[Authorize]
[Route("Admin/Billing")]
public sealed class BillingController : Controller
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService) => _billingService = billingService;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(OrizonClaimTypes.TenantId)?.Value, out Guid tenantId))
        {
            return Forbid();
        }

        TenantBillingDto? billing = await _billingService.GetTenantBillingAsync(tenantId, cancellationToken);
        return billing is null ? View("NoSubscription") : View(billing);
    }
}
