using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Accounts;

public interface ITenantUserService
{
    Task<IReadOnlyCollection<UserAccountDto>> SearchAsync(
        Guid tenantId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<UserAccountDto?> GetAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        UpdateTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(
        Guid tenantId,
        Guid userId,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default);
}
