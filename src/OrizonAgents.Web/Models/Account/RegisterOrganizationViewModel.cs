using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Account;

public sealed class RegisterOrganizationViewModel
{
    [Required(ErrorMessage = "Informe o nome da organização.")]
    [StringLength(150, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome da organização")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o slug da organização.")]
    [StringLength(100, ErrorMessage = "O slug deve ter no máximo {1} caracteres.")]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use letras minúsculas, números e hífens.")]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu nome completo.")]
    [StringLength(160, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe uma senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "A confirmação da senha não confere.")]
    [Display(Name = "Confirmar senha")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Você precisa aceitar os termos.")]
    [Display(Name = "Aceito os termos")]
    public bool AcceptedTerms { get; set; }
}
