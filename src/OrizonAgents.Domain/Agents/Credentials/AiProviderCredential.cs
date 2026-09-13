using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Agents.Credentials;

public sealed class AiProviderCredential
    : AuditableEntity, ITenantOwnedEntity
{
    public const int EncryptedApiKeyMaxLength = 4000;

    private AiProviderCredential()
    {
        EncryptedApiKey = string.Empty;
    }

    public AiProviderCredential(
        Guid tenantId,
        AiProvider provider,
        string encryptedApiKey)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(
                nameof(provider),
                "O provedor de IA informado é inválido.");
        }

        TenantId = tenantId;
        Provider = provider;
        EncryptedApiKey =
            NormalizeEncryptedApiKey(encryptedApiKey);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public AiProvider Provider { get; private set; }

    public string EncryptedApiKey { get; private set; }

    public bool IsActive { get; private set; }

    public void ReplaceApiKey(string encryptedApiKey)
    {
        EncryptedApiKey =
            NormalizeEncryptedApiKey(encryptedApiKey);
        IsActive = true;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeEncryptedApiKey(
        string encryptedApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            encryptedApiKey);

        string normalized = encryptedApiKey.Trim();

        return normalized.Length <= EncryptedApiKeyMaxLength
            ? normalized
            : throw new ArgumentException(
                $"A credencial protegida não pode exceder " +
                $"{EncryptedApiKeyMaxLength} caracteres.",
                nameof(encryptedApiKey));
    }
}
