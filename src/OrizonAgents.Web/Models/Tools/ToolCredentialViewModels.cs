using System.ComponentModel.DataAnnotations;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Web.Models.Tools;

public sealed class ToolCredentialCreateViewModel
{
    [Required(ErrorMessage = "Informe o nome da credencial.")]
    [StringLength(100)]
    [Display(Name = "Nome")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tipo de autenticação")]
    public ToolAuthenticationType AuthenticationType { get; set; }

    [StringLength(100)]
    [Display(Name = "Nome do header")]
    public string? HeaderName { get; set; }

    [Required(ErrorMessage = "Informe o secret.")]
    [DataType(DataType.Password)]
    [Display(Name = "Secret")]
    public string Secret { get; set; } = string.Empty;
}

public sealed class ToolCredentialsPageViewModel
{
    public ToolCredentialCreateViewModel Create { get; set; } = new();
    public IReadOnlyList<ToolCredentialListItemDto> Credentials { get; set; } =
        Array.Empty<ToolCredentialListItemDto>();
}
