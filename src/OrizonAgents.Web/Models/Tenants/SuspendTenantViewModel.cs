using System.ComponentModel.DataAnnotations;

namespace OrizonAgents.Web.Models.Tenants;

public sealed class SuspendTenantViewModel
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o motivo da suspensão.")]
    [StringLength(500, ErrorMessage = "O motivo deve ter no máximo {1} caracteres.")]
    [Display(Name = "Motivo")]
    public string Reason { get; set; } = string.Empty;
}
