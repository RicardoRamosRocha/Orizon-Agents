using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Agents;

public sealed class AgentExecutionUsage : Entity, ITenantOwnedEntity
{
    private AgentExecutionUsage()
    {
    }

    public AgentExecutionUsage(
        Guid tenantId,
        Guid agentId,
        Guid? conversationId,
        string provider,
        string model,
        DateTime startedAtUtc,
        DateTime completedAtUtc,
        long durationMs,
        bool succeeded,
        int modelCallCount,
        int toolExecutionCount,
        int toolSuccessCount,
        int toolFailureCount,
        int toolApprovalRequiredCount,
        int ragResultCount,
        int toolContextCharacters,
        int contextReductionCharacters,
        long? inputTokens,
        long? outputTokens,
        long? totalTokens)
    {
        if (tenantId == Guid.Empty || agentId == Guid.Empty)
        {
            throw new ArgumentException("TenantId e AgentId são obrigatórios.");
        }

        TenantId = tenantId;
        AgentId = agentId;
        ConversationId = conversationId;
        Provider = Required(provider, nameof(provider));
        Model = Required(model, nameof(model));
        StartedAtUtc = EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        CompletedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        DurationMs = Math.Max(0, durationMs);
        Succeeded = succeeded;
        ModelCallCount = modelCallCount;
        ToolExecutionCount = toolExecutionCount;
        ToolSuccessCount = toolSuccessCount;
        ToolFailureCount = toolFailureCount;
        ToolApprovalRequiredCount = toolApprovalRequiredCount;
        RagResultCount = ragResultCount;
        ToolContextCharacters = toolContextCharacters;
        ContextReductionCharacters = contextReductionCharacters;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
    }

    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public DateTime StartedAtUtc { get; private set; }
    public DateTime CompletedAtUtc { get; private set; }
    public long DurationMs { get; private set; }
    public bool Succeeded { get; private set; }
    public int ModelCallCount { get; private set; }
    public int ToolExecutionCount { get; private set; }
    public int ToolSuccessCount { get; private set; }
    public int ToolFailureCount { get; private set; }
    public int ToolApprovalRequiredCount { get; private set; }
    public int RagResultCount { get; private set; }
    public int ToolContextCharacters { get; private set; }
    public int ContextReductionCharacters { get; private set; }
    public long? InputTokens { get; private set; }
    public long? OutputTokens { get; private set; }
    public long? TotalTokens { get; private set; }

    private static string Required(string value, string parameterName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Valor obrigatório.", parameterName);

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("A data deve estar em UTC.", parameterName);
}
