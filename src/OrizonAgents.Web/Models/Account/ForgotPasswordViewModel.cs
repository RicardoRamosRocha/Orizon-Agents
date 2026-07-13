using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Account;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;
}
