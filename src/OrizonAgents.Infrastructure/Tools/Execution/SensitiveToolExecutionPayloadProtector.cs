using Microsoft.AspNetCore.DataProtection;
using System.Text;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class SensitiveToolExecutionPayloadProtector(
    IDataProtectionProvider provider) : ISensitiveToolExecutionPayloadProtector
{
    public const int CanonicalArgumentsMaxBytes = 8192;

    public Task<string> ProtectAsync(
        Guid tenantId,
        Guid executionId,
        string canonicalArguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentifiers(tenantId, executionId);
        ValidateCanonicalArguments(canonicalArguments);

        string protectedArguments = CreateProtector(tenantId, executionId)
            .Protect(canonicalArguments);

        if (protectedArguments.Length > SensitiveToolExecution.ProtectedArgumentsMaxLength)
        {
            throw new InvalidOperationException("Protected arguments exceed the permitted limit.");
        }

        return Task.FromResult(protectedArguments);
    }

    public Task<string> UnprotectAsync(
        Guid tenantId,
        Guid executionId,
        string protectedArguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentifiers(tenantId, executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedArguments);

        return Task.FromResult(CreateProtector(tenantId, executionId)
            .Unprotect(protectedArguments));
    }

    private IDataProtector CreateProtector(Guid tenantId, Guid executionId) =>
        provider.CreateProtector(
            "OrizonAgents.SensitiveToolExecutions.Arguments.v1",
            tenantId.ToString("N"),
            executionId.ToString("N"));

    private static void ValidateIdentifiers(Guid tenantId, Guid executionId)
    {
        if (tenantId == Guid.Empty || executionId == Guid.Empty)
        {
            throw new ArgumentException("Tenant and execution identifiers are required.");
        }
    }

    private static void ValidateCanonicalArguments(string canonicalArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalArguments);

        if (Encoding.UTF8.GetByteCount(canonicalArguments) > CanonicalArgumentsMaxBytes)
        {
            throw new ArgumentException(
                "Canonical arguments exceed the permitted limit.",
                nameof(canonicalArguments));
        }
    }
}
