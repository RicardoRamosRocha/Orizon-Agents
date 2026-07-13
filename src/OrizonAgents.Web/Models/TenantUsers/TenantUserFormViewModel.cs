using System.ComponentModel.DataAnnotations;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Web.Models.TenantUsers;

public sealed class TenantUserFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(160, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Senha temporária")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "A confirmação da senha não confere.")]
    [Display(Name = "Confirmar senha temporária")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o papel.")]
    [Display(Name = "Papel")]
    public string Role { get; set; } = OrizonRoles.TenantMember;

    [Display(Name = "Usuário ativo")]
    public bool IsActive { get; set; } = true;
}
