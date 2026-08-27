namespace OrizonAgents.Application.Knowledge.Documents;

public interface IKnowledgeFileStorage
{
    Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
