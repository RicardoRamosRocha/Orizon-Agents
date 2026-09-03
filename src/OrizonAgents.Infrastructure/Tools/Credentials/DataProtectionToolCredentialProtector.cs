using Microsoft.AspNetCore.DataProtection;

namespace OrizonAgents.Infrastructure.Tools.Credentials;

public sealed class DataProtectionToolCredentialProtector : IToolCredentialProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionToolCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("OrizonAgents.ToolCredentials.Secrets.v1");
    }

    public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return _protector.Protect(secret.Trim());
    }

    public string Unprotect(string encryptedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedSecret);
        return _protector.Unprotect(encryptedSecret);
    }
}
