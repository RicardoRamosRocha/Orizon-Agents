using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Application.Integrations.Requests;

// Tenant identity comes exclusively from ICurrentTenant, never from submitted data.
public sealed record CreateIntegrationConnectionRequest(string Name, IntegrationProvider Provider);
public sealed record UpdateIntegrationConnectionRequest(string Name);