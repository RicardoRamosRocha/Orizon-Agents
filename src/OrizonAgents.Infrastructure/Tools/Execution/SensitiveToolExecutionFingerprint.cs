using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Tools.Execution;

internal static class SensitiveToolExecutionFingerprint
{
    // The input fingerprint is intentionally computed from Data Protection output,
    // never from the canonical arguments. This keeps the persisted value opaque to
    // an offline database reader while retaining a stable identifier for that exact
    // frozen payload.
    public static string ComputeProtectedArguments(string protectedArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedArguments);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(protectedArguments)));
    }

    // Detects semantic or policy changes before a future durable execution. It
    // deliberately excludes credential identity and all secret material so normal
    // credential rotation does not invalidate an approved operation.
    public static string ComputeToolConfiguration(
        Guid tenantId,
        Guid agentId,
        AgentToolBinding binding,
        AgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(tool);

        string? canonicalInputSchema =
            ToolExecutionInputHasher.CanonicalizeSchema(tool.InputSchema);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("tenantId", tenantId);
            writer.WriteString("agentId", agentId);
            writer.WriteString("bindingId", binding.Id);
            writer.WriteString("toolId", tool.Id);
            writer.WriteString("toolKind", tool.Kind.ToString());
            writer.WriteString("riskLevel", tool.RiskLevel.ToString());

            if (tool.IntegrationConnectionId.HasValue)
            {
                writer.WriteString("integrationConnectionId", tool.IntegrationConnectionId.Value);
            }
            else
            {
                writer.WriteNull("integrationConnectionId");
            }

            if (tool.Kind == AgentToolKind.Http)
            {
                writer.WriteString("endpoint", tool.Endpoint);
                writer.WriteString("httpMethod", tool.HttpMethod);
            }

            writer.WritePropertyName("inputSchema");
            if (canonicalInputSchema is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteRawValue(canonicalInputSchema);
            }
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
