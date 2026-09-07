using OrizonAgents.Application.Agents.Execution.Models;

using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Application.Agents.Execution;

public interface IAiChatProvider
{
    string ProviderName { get; }

    AiChatToolInvocationMode ToolInvocationMode =>
        AiChatToolInvocationMode.TextProtocol;

    Task<AiChatCompletionResult> CompleteAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        string? operationalContext = null,
        CancellationToken cancellationToken = default);

    Task<AiChatCompletionResult> CompleteWithToolsAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        IReadOnlyList<AgentToolDefinition> tools,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        return CompleteAsync(
            model,
            systemPrompt,
            userMessage,
            history,
            temperature,
            operationalContext,
            cancellationToken);
    }

    Task<AiChatCompletionResult> ContinueWithToolsAsync(
        string model,
        string systemPrompt,
        double temperature,
        IReadOnlyList<AgentToolDefinition> tools,
        string continuationToken,
        IReadOnlyList<AgentToolResult> toolResults,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"O provedor {ProviderName} não oferece continuação estruturada de Tools.");
    }}
