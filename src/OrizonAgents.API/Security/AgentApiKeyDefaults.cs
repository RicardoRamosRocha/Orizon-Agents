namespace OrizonAgents.API.Security;

public static class AgentApiKeyDefaults
{
    public const string AuthenticationScheme = "AgentApiKey";
    public const string AuthorizationPolicy = "AgentApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string LegacyHeaderName = "X-Orizon-Api-Key";
}
