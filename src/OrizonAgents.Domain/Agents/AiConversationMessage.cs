using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Agents;

public sealed class AiConversationMessage : AuditableEntity, ITenantOwnedEntity
{
    private AiConversationMessage()
    {
    }

    internal AiConversationMessage(
        Guid tenantId,
        Guid conversationId,
        AiMessageRole role,
        string content)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException(
                "ConversationId é obrigatório.",
                nameof(conversationId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "O conteúdo da mensagem é obrigatório.",
                nameof(content));
        }

        TenantId = tenantId;
        ConversationId = conversationId;
        Role = role;
        Content = content.Trim();
    }

    public Guid TenantId { get; private set; }

    public Guid ConversationId { get; private set; }

    public AiMessageRole Role { get; private set; }

    public string Content { get; private set; } = string.Empty;
}
