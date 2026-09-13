namespace OrizonAgents.Infrastructure.Tools.Execution;

public interface IAgentToolEndpointPolicy
{
    Task<bool> IsAllowedAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default);
}
