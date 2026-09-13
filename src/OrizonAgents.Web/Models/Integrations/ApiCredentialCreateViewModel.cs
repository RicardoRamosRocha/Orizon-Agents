using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Integrations;

public sealed class ApiCredentialCreateViewModel
{
    [Required(ErrorMessage = "Selecione o agente.")]
    [Display(Name = "Agente")]
    public Guid? AgentId { get; set; }

    [Required(ErrorMessage = "Informe um nome para a integração.")]
    [StringLength(150)]
    [Display(Name = "Nome da integração")]
    public string Name { get; set; } = string.Empty;

    public string? CreatedApiKey { get; set; }
}
