namespace OrizonAgents.Domain.Common;

public interface ITenantOwnedEntity
{
    Guid TenantId { get; }
}
