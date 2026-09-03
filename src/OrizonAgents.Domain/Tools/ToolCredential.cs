using System.Text.RegularExpressions;
using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tools;

public sealed partial class ToolCredential : AuditableEntity, ITenantOwnedEntity
{
    public const int NameMaxLength = 100;
    public const int HeaderNameMaxLength = 100;
    public const int EncryptedSecretMaxLength = 4000;

    private ToolCredential()
    {
        Name = string.Empty;
        HeaderName = string.Empty;
        EncryptedSecret = string.Empty;
    }

    public ToolCredential(
        Guid tenantId,
        string name,
        ToolAuthenticationType authenticationType,
        string? headerName,
        string encryptedSecret)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (!Enum.IsDefined(authenticationType))
        {
            throw new ArgumentOutOfRangeException(nameof(authenticationType));
        }

        TenantId = tenantId;
        Name = NormalizeRequired(name, NameMaxLength, nameof(name));
        AuthenticationType = authenticationType;
        HeaderName = NormalizeHeaderName(authenticationType, headerName);
        EncryptedSecret = NormalizeRequired(
            encryptedSecret,
            EncryptedSecretMaxLength,
            nameof(encryptedSecret));
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public ToolAuthenticationType AuthenticationType { get; private set; }
    public string HeaderName { get; private set; }
    public string EncryptedSecret { get; private set; }
    public bool IsActive { get; private set; }
    public ICollection<AgentTool> Tools { get; private set; } = new List<AgentTool>();

    public void ReplaceSecret(string encryptedSecret)
    {
        EncryptedSecret = NormalizeRequired(
            encryptedSecret,
            EncryptedSecretMaxLength,
            nameof(encryptedSecret));
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private static string NormalizeHeaderName(
        ToolAuthenticationType authenticationType,
        string? headerName)
    {
        if (authenticationType == ToolAuthenticationType.BearerToken)
        {
            if (!string.IsNullOrWhiteSpace(headerName) &&
                !string.Equals(headerName.Trim(), "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "BearerToken utiliza exclusivamente o header Authorization.",
                    nameof(headerName));
            }

            return "Authorization";
        }

        string normalized = NormalizeRequired(
            headerName ?? string.Empty,
            HeaderNameMaxLength,
            nameof(headerName));

        if (!HeaderNamePattern().IsMatch(normalized) ||
            new[]
            {
                "Authorization", "Proxy-Authorization", "Host",
                "Content-Length", "Transfer-Encoding", "Connection"
            }.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Nome de header de API key inválido.", nameof(headerName));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor é obrigatório.", parameterName);
        }

        string normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException(
                $"O valor excede {maxLength} caracteres.",
                parameterName);
    }

    [GeneratedRegex("^[!#$%&'*+\\-.^_`|~0-9A-Za-z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNamePattern();
}
