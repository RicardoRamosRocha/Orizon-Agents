using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Dashboards;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("Platform/Dashboard")]
public sealed class PlatformDashboardController : Controller
{
    private readonly IDashboardQueryService _dashboardQueryService;

    public PlatformDashboardController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _dashboardQueryService.GetPlatformDashboardAsync(cancellationToken));
    }
}
