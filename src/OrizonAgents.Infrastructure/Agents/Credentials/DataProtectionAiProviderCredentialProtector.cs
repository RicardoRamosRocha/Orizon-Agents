using Microsoft.AspNetCore.DataProtection;

namespace OrizonAgents.Infrastructure.Agents.Credentials;

public sealed class DataProtectionAiProviderCredentialProtector
    : IAiProviderCredentialProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionAiProviderCredentialProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(
            "OrizonAgents.AiProviderCredentials.ApiKeys.v1");
    }

    public string Protect(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return _protector.Protect(apiKey.Trim());
    }

    public string Unprotect(string encryptedApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            encryptedApiKey);

        return _protector.Unprotect(encryptedApiKey);
    }
}
