namespace OrizonAgents.Application.Tools.Execution;

public interface ISensitiveToolExecutionPayloadProtector
{
    Task<string> ProtectAsync(
        Guid tenantId,
        Guid executionId,
        string canonicalArguments,
        CancellationToken cancellationToken = default);

    Task<string> UnprotectAsync(
        Guid tenantId,
        Guid executionId,
        string protectedArguments,
        CancellationToken cancellationToken = default);
}
