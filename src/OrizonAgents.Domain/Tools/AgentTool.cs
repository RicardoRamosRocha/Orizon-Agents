using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tools;

public sealed class AgentTool : AuditableEntity, ITenantOwnedEntity
{
    private AgentTool() { }

    public AgentTool(
        Guid tenantId,
        string name,
        string description,
        string endpoint,
        string httpMethod = "POST")
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        TenantId = tenantId;
        Name = NormalizeRequired(name, 100, nameof(name));
        Description = NormalizeRequired(description, 500, nameof(description));
        Endpoint = NormalizeRequired(endpoint, 2000, nameof(endpoint));
        HttpMethod = NormalizeHttpMethod(httpMethod);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string HttpMethod { get; private set; } = "POST";
    public AgentToolRiskLevel RiskLevel { get; private set; } = AgentToolRiskLevel.Read;
    public string? InputSchema { get; private set; }
    public Guid? ToolCredentialId { get; private set; }
    public ToolCredential? ToolCredential { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(
        string name,
        string description,
        string endpoint,
        string httpMethod,
        string? inputSchema,
        Guid? toolCredentialId,
        AgentToolRiskLevel riskLevel)
    {
        Name = NormalizeRequired(name, 100, nameof(name));
        Description = NormalizeRequired(description, 500, nameof(description));
        Endpoint = NormalizeRequired(endpoint, 2000, nameof(endpoint));
        HttpMethod = NormalizeHttpMethod(httpMethod);
        InputSchema = NormalizeOptional(inputSchema);
        SetCredential(toolCredentialId);
        SetRiskLevel(riskLevel);
    }

    public void SetRiskLevel(AgentToolRiskLevel riskLevel)
    {
        if (!Enum.IsDefined(riskLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(riskLevel),
                riskLevel,
                "Nível de risco da Tool inválido.");
        }

        RiskLevel = riskLevel;
    }

    public void SetCredential(Guid? toolCredentialId)
    {
        if (toolCredentialId == Guid.Empty)
        {
            throw new ArgumentException("ToolCredentialId inválido.", nameof(toolCredentialId));
        }

        ToolCredentialId = toolCredentialId;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor é obrigatório.", parameterName);
        }

        string normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException($"O valor excede {maxLength} caracteres.", parameterName);
    }

    private static string NormalizeHttpMethod(string value)
    {
        string normalized = NormalizeRequired(value, 10, nameof(value)).ToUpperInvariant();
        return normalized switch
        {
            "GET" or "POST" or "PUT" or "PATCH" or "DELETE" => normalized,
            _ => throw new ArgumentException("Método HTTP não suportado.", nameof(value))
        };
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
