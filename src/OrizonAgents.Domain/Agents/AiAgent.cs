using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Agents;

public sealed class AiAgent : AuditableEntity, ITenantOwnedEntity
{
    private AiAgent()
    {
    }

    public AiAgent(
        Guid tenantId,
        string name,
        string systemPrompt,
        AiProvider provider,
        string model)
    {
        TenantId = tenantId;
        Name = name.Trim();
        SystemPrompt = systemPrompt.Trim();
        Provider = provider;
        Model = model.Trim();
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string SystemPrompt { get; private set; } = string.Empty;

    public AiProvider Provider { get; private set; }

    public string Model { get; private set; } = string.Empty;

    public double Temperature { get; private set; } = 0.7;

    public bool IsActive { get; private set; }

    public void Update(
        string name,
        string? description,
        string systemPrompt,
        AiProvider provider,
        string model,
        double temperature)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        SystemPrompt = systemPrompt.Trim();
        Provider = provider;
        Model = model.Trim();
        Temperature = Math.Clamp(temperature, 0, 2);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
