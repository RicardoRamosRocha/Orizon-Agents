using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Account;

public sealed class EditProfileViewModel
{
    [Required(ErrorMessage = "Informe seu nome completo.")]
    [StringLength(160, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome completo")]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? TenantName { get; set; }

    public string? TenantSlug { get; set; }
}
