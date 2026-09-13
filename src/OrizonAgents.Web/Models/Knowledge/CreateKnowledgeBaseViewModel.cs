using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Knowledge;

public sealed class CreateKnowledgeBaseViewModel
{
    [Required(ErrorMessage = "Informe o nome da base.")]
    [StringLength(160)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }
}
