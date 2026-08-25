using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Integrations;

public sealed class ApiCredential : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 150;
    public const int KeyHashMaxLength = 128;

    private ApiCredential()
    {
        Name = string.Empty;
        KeyHash = string.Empty;
    }

    public ApiCredential(
        Guid tenantId,
        string name,
        string keyHash)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        TenantId = tenantId;
        Name = NormalizeName(name);
        KeyHash = NormalizeKeyHash(keyHash);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string KeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
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
}
