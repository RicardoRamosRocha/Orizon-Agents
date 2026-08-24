using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Agents;

public sealed class AiConversation : AuditableEntity, ITenantOwnedEntity
{
    private readonly List<AiConversationMessage> _messages = [];

    private AiConversation()
    {
    }

    public AiConversation(
        Guid tenantId,
        Guid agentId,
        string? title = null)
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
        Title = NormalizeTitle(title);
    }

    public Guid TenantId { get; private set; }

    public Guid AgentId { get; private set; }

    public string? Title { get; private set; }

    public IReadOnlyCollection<AiConversationMessage> Messages =>
        _messages.AsReadOnly();

    public void Rename(string? title)
    {
        Title = NormalizeTitle(title);
    }

    public AiConversationMessage AddUserMessage(string content)
    {
        return AddMessage(AiMessageRole.User, content);
    }

    public AiConversationMessage AddAssistantMessage(string content)
    {
        return AddMessage(AiMessageRole.Assistant, content);
    }

    private AiConversationMessage AddMessage(
        AiMessageRole role,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "O conteúdo da mensagem é obrigatório.",
                nameof(content));
        }

        var message = new AiConversationMessage(
            TenantId,
            Id,
            role,
            content);

        _messages.Add(message);

        return message;
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string normalized = title.Trim();

        return normalized.Length <= 150
            ? normalized
            : normalized[..150];
    }
}
