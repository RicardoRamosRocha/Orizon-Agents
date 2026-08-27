using Microsoft.AspNetCore.Mvc;

namespace OrizonAgents.API.Controllers;

[ApiController]
[Route("api/test-tools")]
public sealed class TestToolsController : ControllerBase
{
    [HttpPost("operational-status")]
    public IActionResult GetOperationalStatus()
    {
        return Ok(new
        {
            system = "Orizon Sandbox",
            status = "operational",
            code = "ORIZON-7429",
            message = "Integração de ferramentas funcionando corretamente."
        });
    }
}
