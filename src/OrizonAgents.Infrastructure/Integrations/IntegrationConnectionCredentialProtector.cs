using Microsoft.AspNetCore.DataProtection;

namespace OrizonAgents.Infrastructure.Integrations;

// Uses the existing Data Protection key ring, scoped to the owner and connection.
// Future provider handlers protect credentials here before passing them to the domain.
public sealed class IntegrationConnectionCredentialProtector(IDataProtectionProvider provider)
{
    public string Protect(Guid tenantId, Guid connectionId, string credentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentials);
        return CreateProtector(tenantId, connectionId).Protect(credentials);
    }

    public string Unprotect(Guid tenantId, Guid connectionId, string encryptedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        return CreateProtector(tenantId, connectionId).Unprotect(encryptedCredentials);
    }

    private IDataProtector CreateProtector(Guid tenantId, Guid connectionId)
    {
        if (tenantId == Guid.Empty || connectionId == Guid.Empty)
        {
            throw new ArgumentException("Tenant e conexão são obrigatórios para proteger credenciais.");
        }

        return provider.CreateProtector(
            "OrizonAgents.IntegrationConnections.Credentials.v1",
            tenantId.ToString("N"), connectionId.ToString("N"));
    }
}