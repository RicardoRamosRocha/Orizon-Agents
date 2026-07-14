using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.WhatsApp;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("Platform/WhatsApp")]
public sealed class PlatformWhatsAppController : Controller
{
    private readonly IWhatsAppPlatformService _platformService;

    public PlatformWhatsAppController(IWhatsAppPlatformService platformService)
    {
        _platformService = platformService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _platformService.GetOverviewAsync(cancellationToken));
    }
}
