using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.API.Controllers;

[ApiController]
[Route("api/agents")]
public sealed class AgentsController : ControllerBase
{
    private const int MaxRequestBytes = 256 * 1024;

    private readonly IAiAgentRunner _aiAgentRunner;

    public AgentsController(IAiAgentRunner aiAgentRunner)
    {
        _aiAgentRunner = aiAgentRunner;
    }

    [HttpPost("{agentId:guid}/run")]
    [RequestSizeLimit(MaxRequestBytes)]
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

        if (request.Message.Length > 12000)
        {
            return BadRequest(new
            {
                error = "A mensagem excede o limite permitido."
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
                error = result.FirstError
                    ?? "Não foi possível executar o agente."
            });
        }

        return Ok(result.Value);
    }
}
