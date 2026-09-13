using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Integrations;

public sealed class ApiCredential : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 150;
    public const int KeyIdentifierMaxLength = 32;
    public const int KeyHashMaxLength = 128;

    private ApiCredential()
    {
        Name = string.Empty;
        KeyIdentifier = null;
        KeyHash = string.Empty;
    }

    public ApiCredential(
        Guid tenantId,
        Guid agentId,
        string name,
        string keyIdentifier,
        string keyHash)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "AgentId é obrigatório.",
                nameof(agentId));
        }

        TenantId = tenantId;
        AgentId = agentId;
        Name = NormalizeName(name);
        KeyIdentifier = NormalizeKeyIdentifier(keyIdentifier);
        KeyHash = NormalizeKeyHash(keyHash);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public Guid? AgentId { get; private set; }

    public AiAgent? Agent { get; private set; }

    public string Name { get; private set; }

    public string? KeyIdentifier { get; private set; }

    public string KeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsureUtc(revokedAtUtc);

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAtUtc = revokedAtUtc;
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalized = name.Trim();

        return normalized.Length <= NameMaxLength
            ? normalized
            : throw new ArgumentException(
                $"O nome não pode exceder {NameMaxLength} caracteres.",
                nameof(name));
    }

    private static string NormalizeKeyHash(string keyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        string normalized = keyHash.Trim();

        return normalized.Length <= KeyHashMaxLength
            ? normalized
            : throw new ArgumentException(
                $"O hash da chave não pode exceder {KeyHashMaxLength} caracteres.",
                nameof(keyHash));
    }

    private static string NormalizeKeyIdentifier(string keyIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyIdentifier);

        string normalized = keyIdentifier.Trim();

        return normalized.Length <= KeyIdentifierMaxLength
            ? normalized
            : throw new ArgumentException(
                $"O identificador da chave não pode exceder {KeyIdentifierMaxLength} caracteres.",
                nameof(keyIdentifier));
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "A data de revogação deve estar em UTC.",
                nameof(dateTime));
        }
    }
}
