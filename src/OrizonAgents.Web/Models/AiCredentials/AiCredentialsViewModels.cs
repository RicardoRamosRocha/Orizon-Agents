using System.ComponentModel.DataAnnotations;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Web.Models.AiCredentials;

public sealed class AiProviderCredentialViewModel
{
    public AiProvider Provider { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public bool IsConfigured { get; set; }

}

public sealed class AiCredentialsIndexViewModel
{
    public IReadOnlyList<AiProviderCredentialViewModel> Providers { get; set; }
        = Array.Empty<AiProviderCredentialViewModel>();
}
