using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;

namespace OrizonAgents.Application.Tenants;

public interface ITenantManagementService
{
    Task<PagedResult<TenantListItemDto>> ListAsync(
        TenantListRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantDetailsDto?> GetDetailsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantOrganizationDto?> GetOrganizationAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SuspendAsync(
        SuspendTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ReactivateAsync(
        ReactivateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateOwnSettingsAsync(
        UpdateOwnTenantSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsTenantSuspendedAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
