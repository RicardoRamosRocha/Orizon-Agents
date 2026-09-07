using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Application.Agents.Execution.Telemetry;

public interface IAgentExecutionTelemetry
{
    IAgentExecutionTelemetrySession Start(
        AgentExecutionTelemetryStart start);
}

public interface IAgentExecutionTelemetrySession
{
    void RecordModelCall();

    void RecordModelUsage(AiChatUsage? usage);

    void RecordToolExecution(bool succeeded, bool approvalRequired);

    void RecordRagResults(int count);

    void RecordToolContext(int originalCharacters, int usedCharacters);

    Task CompleteAsync(
        Guid? conversationId,
        bool succeeded,
        CancellationToken cancellationToken = default);
}

public sealed record AgentExecutionTelemetryStart(
    Guid TenantId,
    Guid AgentId,
    string Provider,
    string Model);
