using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Tools;

public sealed class AgentToolFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da Tool.")]
    [StringLength(100)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição da Tool.")]
    [StringLength(500)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endpoint.")]
    [StringLength(2000)]
    [Display(Name = "Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Método HTTP")]
    public string HttpMethod { get; set; } = "POST";

    [Display(Name = "Schema de entrada (JSON)")]
    public string? InputSchema { get; set; }

    public bool IsActive { get; set; } = true;
}
