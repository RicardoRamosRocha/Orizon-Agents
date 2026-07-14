namespace OrizonAgents.Infrastructure.WhatsApp;

public interface IWhatsAppTokenProtector
{
    string Protect(string accessToken);

    string Unprotect(string encryptedAccessToken);

    string Mask(string? encryptedAccessToken);
}
