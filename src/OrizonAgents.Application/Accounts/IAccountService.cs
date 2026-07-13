using OrizonAgents.Application.Accounts.Models;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Accounts;

public interface IAccountService
{
    Task<OperationResult<Guid>> RegisterOrganizationAsync(
        RegisterOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> PasswordSignInAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task SignOutAsync();

    Task<OperationResult> SendPasswordResetAsync(
        ForgotPasswordRequest request,
        Func<string, string, string> resetLinkFactory,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<ProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
