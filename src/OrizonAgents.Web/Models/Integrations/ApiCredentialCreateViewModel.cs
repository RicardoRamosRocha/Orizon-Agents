using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Integrations;

public sealed class ApiCredentialCreateViewModel
{
    [Required(ErrorMessage = "Informe um nome para a integração.")]
    [StringLength(150)]
    [Display(Name = "Nome da integração")]
    public string Name { get; set; } = string.Empty;

    public string? CreatedApiKey { get; set; }
}
