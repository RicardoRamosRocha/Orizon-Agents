using System.Text.Json.Serialization;

namespace OrizonAgents.Infrastructure.Integrations.Google;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Integrations:Google";
    public string ClientId { get; set; } = string.Empty;
    [JsonIgnore]
    public string ClientSecret { get; set; } = string.Empty;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}