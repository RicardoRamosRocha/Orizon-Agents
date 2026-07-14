using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppTemplateService
{
    Task<PagedResult<WhatsAppTemplateDto>> ListTemplatesAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult<int>> SynchronizeTemplatesAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);
}
