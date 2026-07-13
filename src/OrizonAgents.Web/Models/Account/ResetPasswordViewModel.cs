using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Account;

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "A confirmação da senha não confere.")]
    [Display(Name = "Confirmar nova senha")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
