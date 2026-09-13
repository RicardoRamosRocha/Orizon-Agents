using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Knowledge.Documents;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Storage;

public sealed class LocalKnowledgeFileStorage : IKnowledgeFileStorage
{
    private readonly string _rootPath;

    public LocalKnowledgeFileStorage(IConfiguration configuration)
    {
        string configuredPath =
            configuration["Knowledge:StoragePath"]
            ?? "App_Data/knowledge";

        _rootPath = Path.GetFullPath(configuredPath);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant id is required.",
                nameof(tenantId));
        }

        string extension = Path.GetExtension(fileName);

        string storageKey = Path.Combine(
            tenantId.ToString("N"),
            $"{Guid.NewGuid():N}{extension}");

        string fullPath = ResolvePath(storageKey);

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        await using FileStream destination =
            new(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

        await content.CopyToAsync(
            destination,
            cancellationToken);

        return storageKey.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException(
                "Storage key is required.",
                nameof(storageKey));
        }

        string normalizedKey = storageKey.Replace(
            '/',
            Path.DirectorySeparatorChar);

        string fullPath = Path.GetFullPath(
            Path.Combine(_rootPath, normalizedKey));

        string rootPrefix =
            _rootPath.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Invalid knowledge storage key.");
        }

        return fullPath;
    }
}
