namespace OrizonAgents.Infrastructure.Agents.Credentials;

public interface IAiProviderCredentialProtector
{
    string Protect(string apiKey);

    string Unprotect(string encryptedApiKey);
}
