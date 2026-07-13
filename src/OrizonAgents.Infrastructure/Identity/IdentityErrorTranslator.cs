using Microsoft.AspNetCore.Identity;

namespace OrizonAgents.Infrastructure.Identity;

internal static class IdentityErrorTranslator
{
    public static string[] Translate(IEnumerable<IdentityError> errors)
    {
        return errors.Select(error => error.Code switch
        {
            "DuplicateEmail" => "Este e-mail já está em uso.",
            "DuplicateUserName" => "Este e-mail já está em uso.",
            "InvalidEmail" => "Informe um e-mail válido.",
            "PasswordTooShort" => "A senha informada é muito curta.",
            "PasswordRequiresNonAlphanumeric" => "A senha deve conter ao menos um caractere especial.",
            "PasswordRequiresDigit" => "A senha deve conter ao menos um número.",
            "PasswordRequiresUpper" => "A senha deve conter ao menos uma letra maiúscula.",
            "PasswordMismatch" => "A senha atual não confere.",
            _ => error.Description
        }).ToArray();
    }
}
