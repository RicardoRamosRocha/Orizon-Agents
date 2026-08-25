using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.API.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly IAiAgentRunner _aiAgentRunner;

    public AgentsController(IAiAgentRunner aiAgentRunner)
    {
        _aiAgentRunner = aiAgentRunner;
    }

    [HttpPost("{agentId:guid}/run")]
    public async Task<IActionResult> Run(
        Guid agentId,
        [FromBody] AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                error = "A mensagem é obrigatória."
            });
        }

        var result = await _aiAgentRunner.RunAsync(
            agentId,
            request,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return BadRequest(new
            {
                error = result.FirstError ?? "Não foi possível executar o agente."
            });
        }

        return Ok(result.Value);
    }
}
