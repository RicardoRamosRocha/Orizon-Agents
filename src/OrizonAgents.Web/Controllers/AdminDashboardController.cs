using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Application.Dashboards.Models;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("Admin/Dashboard")]
public sealed class AdminDashboardController : Controller
{
    private readonly IDashboardQueryService _dashboardQueryService;

    public AdminDashboardController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        Guid userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _dashboardQueryService.GetTenantDashboardAsync(tenantId, userId, cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return View(result.Value);
    }

    private Guid GetTenantId()
    {
        string? value = User.FindFirstValue(OrizonClaimTypes.TenantId);
        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException("TenantAdmin autenticado sem TenantId.");
    }
}
