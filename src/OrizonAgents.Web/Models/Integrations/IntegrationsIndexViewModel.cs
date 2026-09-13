using OrizonAgents.Application.Integrations.Models;

namespace OrizonAgents.Web.Models.Integrations;

public sealed class IntegrationsIndexViewModel
{
    public ApiCredentialCreateViewModel Create { get; set; } = new();

    public IReadOnlyList<ApiCredentialListItem> Credentials { get; set; }
        = Array.Empty<ApiCredentialListItem>();
}
