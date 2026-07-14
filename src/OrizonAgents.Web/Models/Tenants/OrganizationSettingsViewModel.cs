using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Tenants;

public sealed class OrganizationSettingsViewModel
{
    public Guid TenantId { get; set; }

    [Required(ErrorMessage = "Informe o nome da organização.")]
    [StringLength(150, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a cultura.")]
    [StringLength(16)]
    [Display(Name = "Cultura")]
    public string Culture { get; set; } = "pt-BR";

    [Required(ErrorMessage = "Informe o fuso horário.")]
    [StringLength(64)]
    [Display(Name = "Fuso horário")]
    public string TimeZone { get; set; } = "America/Sao_Paulo";

    [StringLength(120)]
    [Display(Name = "Contato")]
    public string? ContactName { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail de contato")]
    public string? ContactEmail { get; set; }

    [StringLength(32)]
    [Display(Name = "Telefone")]
    public string? ContactPhone { get; set; }

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
