using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Knowledge;

public sealed class KnowledgeBase : AuditableEntity, ITenantOwnedEntity
{
    private KnowledgeBase()
    {
    }

    public KnowledgeBase(
        Guid tenantId,
        string name,
        string? description = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant id is required.",
                nameof(tenantId));
        }

        TenantId = tenantId;
        Name = NormalizeRequired(name, nameof(name), 160);
        Description = NormalizeOptional(description, 1000);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<KnowledgeDocument> Documents { get; private set; } =
        new List<KnowledgeDocument>();

    public ICollection<AgentKnowledgeBinding> AgentBindings { get; private set; } =
        new List<AgentKnowledgeBinding>();

    public void Update(string name, string? description)
    {
        Name = NormalizeRequired(name, nameof(name), 160);
        Description = NormalizeOptional(description, 1000);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static string NormalizeRequired(
        string value,
        string parameterName,
        int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                "Value is required.",
                parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
