using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Integrations;

public sealed class IntegrationConnection : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 150;
    public const int EncryptedCredentialsMaxLength = 16000;

    private IntegrationConnection()
    {
        Name = string.Empty;
    }

    public IntegrationConnection(Guid tenantId, string name, IntegrationProvider provider)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), "Provedor inválido.");
        }

        TenantId = tenantId;
        Name = NormalizeName(name);
        Provider = provider;
        Status = IntegrationConnectionStatus.PendingConfiguration;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public IntegrationProvider Provider { get; private set; }
    public IntegrationConnectionStatus Status { get; private set; }
    public bool IsActive { get; private set; }

    // Reserved for a protected provider payload. Administrative operations never accept credentials.
    public string? EncryptedCredentials { get; private set; }

    public void Rename(string name) => Name = NormalizeName(name);
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void ReplaceProtectedCredentials(string encryptedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        if (encryptedCredentials.Length > EncryptedCredentialsMaxLength)
        {
            throw new ArgumentException("Credenciais protegidas excedem o limite permitido.", nameof(encryptedCredentials));
        }

        EncryptedCredentials = encryptedCredentials;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string normalized = name.Trim();
        return normalized.Length <= NameMaxLength
            ? normalized
            : throw new ArgumentException($"O nome não pode exceder {NameMaxLength} caracteres.", nameof(name));
    }
}