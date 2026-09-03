using System.Text.Json;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Execution;

public interface IToolExecutionApprovalService
{
    Task<IReadOnlyList<ToolExecutionApprovalListItemDto>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<ToolExecutionAuthorizationResult> AuthorizeAsync(
        Guid agentId,
        AgentTool tool,
        JsonElement? input,
        CancellationToken cancellationToken = default);

    Task<bool> ApproveAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default);

    Task<bool> RejectAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default);
}
