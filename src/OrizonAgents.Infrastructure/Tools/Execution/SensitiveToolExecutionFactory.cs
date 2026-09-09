using System.Text.Json;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class SensitiveToolExecutionFactory(
    ISensitiveToolExecutionPayloadProtector payloadProtector)
    : ISensitiveToolExecutionFactory
{
    public async Task<SensitiveToolExecution> CreateAsync(
        ToolExecutionApproval approval,
        AgentTool tool,
        AgentToolBinding binding,
        JsonElement? validatedInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(binding);

        if (tool.RiskLevel != AgentToolRiskLevel.Sensitive)
        {
            throw new ArgumentException("A durable execution requires a Sensitive Tool.", nameof(tool));
        }

        if (approval.TenantId != tool.TenantId ||
            approval.AgentId != binding.AgentId ||
            approval.ToolId != tool.Id ||
            binding.TenantId != tool.TenantId ||
            binding.ToolId != tool.Id ||
            !binding.IsActive)
        {
            throw new ArgumentException("The approval, Tool, and binding do not describe the same active operation.");
        }

        Guid executionId = Guid.NewGuid();
        string canonicalArguments = ToolExecutionInputHasher.Canonicalize(validatedInput);
        string protectedArguments = await payloadProtector.ProtectAsync(
            tool.TenantId,
            executionId,
            canonicalArguments,
            cancellationToken);

        return new SensitiveToolExecution(
            executionId,
            tool.TenantId,
            approval,
            binding.AgentId,
            tool.Id,
            binding.Id,
            tool.Kind,
            tool.IntegrationConnectionId,
            protectedArguments,
            SensitiveToolExecutionFingerprint.ComputeProtectedArguments(protectedArguments),
            SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
                tool.TenantId,
                binding.AgentId,
                binding,
                tool),
            approval.ExpiresAtUtc);
    }
}
