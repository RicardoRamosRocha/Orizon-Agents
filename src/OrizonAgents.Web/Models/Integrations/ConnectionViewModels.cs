using System.ComponentModel.DataAnnotations;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Web.Models.Integrations;

public sealed class ConnectionCreateViewModel
{
    [Required(ErrorMessage = "Informe o nome da conexão.")]
    [StringLength(IntegrationConnection.NameMaxLength)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(IntegrationProvider))]
    [Display(Name = "Provedor")]
    public IntegrationProvider Provider { get; set; } = IntegrationProvider.Gmail;
}

public sealed class ConnectionEditViewModel
{
    [Required(ErrorMessage = "Informe o nome da conexão.")]
    [StringLength(IntegrationConnection.NameMaxLength)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;
}

public sealed class ConnectionsPageViewModel
{
    public ConnectionCreateViewModel Create { get; set; } = new();
    public IReadOnlyList<IntegrationConnectionDto> Connections { get; set; } = [];
}

public sealed class ConnectionDetailsViewModel
{
    public required IntegrationConnectionDto Connection { get; init; }
    public ConnectionEditViewModel Edit { get; init; } = new();
    public bool IsGmailReadAuthorized { get; init; }

    public static string StatusLabel(IntegrationConnectionStatus status) => status switch
    {
        IntegrationConnectionStatus.PendingConfiguration => "Pendente de configuração",
        IntegrationConnectionStatus.Disconnected => "Desconectada",
        IntegrationConnectionStatus.Connected => "Conectada",
        IntegrationConnectionStatus.Error => "Erro",
        _ => "Desconhecido"
    };
}
