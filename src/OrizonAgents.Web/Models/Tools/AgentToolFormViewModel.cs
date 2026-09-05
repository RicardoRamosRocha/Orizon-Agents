using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Web.Models.Tools;

public enum AgentToolCategory
{
    Http = 1,
    Gmail = 2
}

public enum GmailToolAction
{
    SearchEmails = 1,
    ReadEmail = 2
}

public sealed class AgentToolFormViewModel
{
    public Guid? Id { get; set; }

    [EnumDataType(typeof(AgentToolCategory), ErrorMessage = "Selecione um tipo de ferramenta válido.")]
    [Display(Name = "Tipo de ferramenta")]
    public AgentToolCategory Category { get; set; } = AgentToolCategory.Http;

    [EnumDataType(typeof(GmailToolAction), ErrorMessage = "Selecione uma ação Gmail válida.")]
    [Display(Name = "Ação")]
    public GmailToolAction GmailAction { get; set; } = GmailToolAction.SearchEmails;

    [Required(ErrorMessage = "Informe o nome da Tool.")]
    [StringLength(100)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a descrição da Tool.")]
    [StringLength(500)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [Display(Name = "Método HTTP")]
    public string HttpMethod { get; set; } = "POST";

    [Required]
    [EnumDataType(
        typeof(AgentToolRiskLevel),
        ErrorMessage = "Selecione um nível de risco válido.")]
    [Display(Name = "Nível de risco")]
    public AgentToolRiskLevel RiskLevel { get; set; } = AgentToolRiskLevel.Read;

    [Display(Name = "Schema de entrada (JSON)")]
    public string? InputSchema { get; set; }

    [Display(Name = "Credencial")]
    public Guid? ToolCredentialId { get; set; }

    [Display(Name = "Conexão")]
    public Guid? IntegrationConnectionId { get; set; }

    public IReadOnlyList<SelectListItem> CredentialOptions { get; set; } =
        Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> GmailConnectionOptions { get; set; } =
        Array.Empty<SelectListItem>();

    public bool IsActive { get; set; } = true;
    public bool IsEdit { get; set; }
}
