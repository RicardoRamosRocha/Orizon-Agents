using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppMessagingService
{
    Task<OperationResult<Guid>> QueueTextAsync(SendWhatsAppTextRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> QueueTemplateAsync(SendWhatsAppTemplateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> QueueMediaAsync(SendWhatsAppMediaRequest request, CancellationToken cancellationToken = default);

    Task<WhatsAppPagedMessagesDto> ListMessagesAsync(WhatsAppMessageListRequest request, CancellationToken cancellationToken = default);
}
