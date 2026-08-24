using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Agents;

public sealed class AiAgentFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do agente.")]
    [StringLength(150)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Informe as instruções do agente.")]
    [StringLength(12000)]
    [Display(Name = "Instruções do agente")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Provedor de IA")]
    public string Provider { get; set; } = "Groq";

    [Required(ErrorMessage = "Informe o modelo.")]
    [StringLength(150)]
    [Display(Name = "Modelo")]
    public string Model { get; set; } = "openai/gpt-oss-20b";

    [Range(0, 2)]
    [Display(Name = "Temperatura")]
    public double Temperature { get; set; } = 0.7;

    public bool IsActive { get; set; } = true;
}

