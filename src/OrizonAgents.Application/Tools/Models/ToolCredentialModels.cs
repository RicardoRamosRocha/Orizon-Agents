using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Models;

public sealed record ToolCredentialListItemDto(
    Guid Id,
    string Name,
    ToolAuthenticationType AuthenticationType,
    string HeaderName,
    bool IsActive);

public sealed record ResolvedToolCredential(
    ToolAuthenticationType AuthenticationType,
    string HeaderName,
    string Secret);
