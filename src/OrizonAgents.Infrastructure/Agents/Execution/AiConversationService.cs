using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class AiConversationService : IAiConversationService
{
    private readonly OrizonAgentsDbContext _dbContext;

    public AiConversationService(
        OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AiConversationDto?> GetAsync(
        Guid conversationId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        AiConversation? conversation =
            await _dbContext.AiConversations
                .AsNoTracking()
                .Include(candidate => candidate.Messages)
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.Id == conversationId &&
                        candidate.AgentId == agentId,
                    cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        IReadOnlyList<AiConversationMessageDto> messages =
            conversation.Messages
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message =>
                    new AiConversationMessageDto(
                        message.Role == AiMessageRole.User
                            ? "user"
                            : "assistant",
                        message.Content,
                        message.CreatedAtUtc))
                .ToList();

        return new AiConversationDto(
            conversation.Id,
            conversation.AgentId,
            conversation.Title,
            messages);
    }
}
