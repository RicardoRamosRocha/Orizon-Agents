using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Tenants;

public sealed class TenantFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da organização.")]
    [StringLength(150, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o slug.")]
    [StringLength(100, ErrorMessage = "O slug deve ter no máximo {1} caracteres.")]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use letras minúsculas, números e hífens.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

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

    [Display(Name = "Administrador")]
    public string AdminFullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail do administrador")]
    public string AdminEmail { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Senha inicial")]
    public string AdminPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(AdminPassword), ErrorMessage = "A confirmação da senha não confere.")]
    [Display(Name = "Confirmar senha")]
    public string AdminConfirmPassword { get; set; } = string.Empty;

    public string ConcurrencyStamp { get; set; } = string.Empty;
}
