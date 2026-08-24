using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Application.Agents.Execution;

public interface IAiChatProvider
{
    string ProviderName { get; }

    Task<string> CompleteAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        CancellationToken cancellationToken = default);
}
