using OrizonAgents.Application.WhatsApp.Models;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppPlatformService
{
    Task<WhatsAppPlatformOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
