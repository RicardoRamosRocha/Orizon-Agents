using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppConnectionService
{
    Task<WhatsAppConnectionSummaryDto> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<WhatsAppConnectionDto?> GetConnectionAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateConnectionAsync(CreateWhatsAppConnectionRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateConnectionAsync(Guid tenantId, Guid connectionId, UpdateWhatsAppConnectionRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> ReplaceTokenAsync(Guid tenantId, Guid connectionId, ReplaceWhatsAppTokenRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> ValidateConnectionAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default);

    Task<OperationResult> SetDefaultAsync(Guid tenantId, Guid connectionId, string concurrencyStamp, CancellationToken cancellationToken = default);

    Task<OperationResult> DisconnectAsync(Guid tenantId, Guid connectionId, string concurrencyStamp, CancellationToken cancellationToken = default);
}
