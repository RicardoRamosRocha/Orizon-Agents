using OrizonAgents.Application.WhatsApp.Models;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppProcessor
{
    Task<WhatsAppProcessorResult> ProcessInboxAsync(CancellationToken cancellationToken = default);

    Task<WhatsAppProcessorResult> ProcessOutboxAsync(CancellationToken cancellationToken = default);

    Task<int> PruneInboxPayloadsAsync(CancellationToken cancellationToken = default);
}
