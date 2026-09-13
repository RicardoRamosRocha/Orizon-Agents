using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Requests;

public sealed record CreateToolCredentialRequest(
    Guid TenantId,
    string Name,
    ToolAuthenticationType AuthenticationType,
    string? HeaderName,
    string Secret);
