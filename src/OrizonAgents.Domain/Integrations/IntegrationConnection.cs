using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Integrations;

public sealed class IntegrationConnection : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 150;
    public const int ConnectedAccountEmailMaxLength = 320;
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

    public string? ConnectedAccountEmail { get; private set; }
    public string? PendingOAuthStateHash { get; private set; }
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");

    // Provider payload is always protected before entering the domain.
    public string? EncryptedCredentials { get; private set; }

    public void Rename(string name) => Name = NormalizeName(name);
    public void Activate() => IsActive = true;
    public void Deactivate()
    {
        IsActive = false;
        PendingOAuthStateHash = null;
        RenewConcurrencyStamp();
    }

    public void BeginOAuth(string stateHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateHash);
        if (stateHash.Length != 64)
        {
            throw new ArgumentException("Hash OAuth inválido.", nameof(stateHash));
        }
        PendingOAuthStateHash = stateHash;
        RenewConcurrencyStamp();
    }

    public void ConsumeOAuthState()
    {
        PendingOAuthStateHash = null;
        RenewConcurrencyStamp();
    }

    public void Connect(string email, string encryptedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (email.Length > ConnectedAccountEmailMaxLength)
        {
            throw new ArgumentException("E-mail da conta excede o limite permitido.", nameof(email));
        }
        ReplaceProtectedCredentials(encryptedCredentials);
        ConnectedAccountEmail = email.Trim();
        Status = IntegrationConnectionStatus.Connected;
        PendingOAuthStateHash = null;
    }

    public void MarkAuthenticationError()
    {
        Status = IntegrationConnectionStatus.Error;
        PendingOAuthStateHash = null;
        RenewConcurrencyStamp();
    }

    public void Disconnect()
    {
        EncryptedCredentials = null;
        ConnectedAccountEmail = null;
        PendingOAuthStateHash = null;
        Status = IntegrationConnectionStatus.Disconnected;
        RenewConcurrencyStamp();
    }

    private void RenewConcurrencyStamp() => ConcurrencyStamp = Guid.NewGuid().ToString("N");

    public void ReplaceProtectedCredentials(string encryptedCredentials)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        if (encryptedCredentials.Length > EncryptedCredentialsMaxLength)
        {
            throw new ArgumentException("Credenciais protegidas excedem o limite permitido.", nameof(encryptedCredentials));
        }

        EncryptedCredentials = encryptedCredentials;
        RenewConcurrencyStamp();
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