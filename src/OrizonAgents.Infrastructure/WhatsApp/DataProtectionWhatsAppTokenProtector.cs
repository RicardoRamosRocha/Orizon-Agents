using Microsoft.AspNetCore.DataProtection;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class DataProtectionWhatsAppTokenProtector : IWhatsAppTokenProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionWhatsAppTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("OrizonAgents.WhatsApp.AccessTokens.v1");
    }

    public string Protect(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) throw new ArgumentException("Token é obrigatório.", nameof(accessToken));
        return _protector.Protect(accessToken.Trim());
    }

    public string Unprotect(string encryptedAccessToken)
    {
        if (string.IsNullOrWhiteSpace(encryptedAccessToken)) throw new ArgumentException("Token protegido é obrigatório.", nameof(encryptedAccessToken));
        return _protector.Unprotect(encryptedAccessToken);
    }

    public string Mask(string? encryptedAccessToken)
    {
        if (string.IsNullOrWhiteSpace(encryptedAccessToken)) return "••••";
        return "••••••••";
    }
}
