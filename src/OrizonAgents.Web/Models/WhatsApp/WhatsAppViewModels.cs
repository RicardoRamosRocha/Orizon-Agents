using System.ComponentModel.DataAnnotations;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.WhatsApp.Models;

namespace OrizonAgents.Web.Models.WhatsApp;

public sealed class WhatsAppConnectionFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da conexão.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o WABA ID.")]
    [Display(Name = "WABA ID")]
    public string WhatsAppBusinessAccountId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o Phone Number ID.")]
    [Display(Name = "Phone Number ID")]
    public string PhoneNumberId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o número.")]
    [Display(Name = "Número")]
    public string DisplayPhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Nome verificado")]
    public string VerifiedName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Token de acesso")]
    public string AccessToken { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? ConcurrencyStamp { get; set; }
}

public sealed class WhatsAppIndexViewModel
{
    public WhatsAppConnectionSummaryDto? Summary { get; set; }

    public WhatsAppConnectionFormViewModel Form { get; set; } = new();
}

public sealed class WhatsAppMessagesViewModel
{
    public string? Direction { get; set; }

    public string? Status { get; set; }

    public int PageNumber { get; set; } = 1;

    public PagedResult<WhatsAppMessageDto>? Result { get; set; }

    public WhatsAppUsageDto? Usage { get; set; }
}

public sealed class WhatsAppTemplatesViewModel
{
    public Guid? ConnectionId { get; set; }

    public string? Status { get; set; }

    public int PageNumber { get; set; } = 1;

    public PagedResult<WhatsAppTemplateDto>? Result { get; set; }

    public IReadOnlyCollection<WhatsAppConnectionDto> Connections { get; set; } = Array.Empty<WhatsAppConnectionDto>();
}
