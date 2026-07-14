using System.ComponentModel.DataAnnotations;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Web.Models.Billing;

public sealed class PlanIndexViewModel
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsPublic { get; set; }
    public int PageNumber { get; set; } = 1;
    public PagedResult<PlanListItemDto>? Result { get; set; }
}

public sealed class PlanFormViewModel
{
    public Guid? Id { get; set; }
    [Required(ErrorMessage = "Informe o nome do plano.")]
    public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "Informe o código.")]
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Range(0, double.MaxValue, ErrorMessage = "Preço mensal não pode ser negativo.")]
    public decimal MonthlyPrice { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "Preço anual não pode ser negativo.")]
    public decimal YearlyPrice { get; set; }
    public string Currency { get; set; } = "BRL";
    [Range(0, 365)]
    public int TrialDays { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public Dictionary<string, EntitlementInputViewModel> Entitlements { get; set; } = [];
}

public sealed class EntitlementInputViewModel
{
    public bool IsEnabled { get; set; }
    public int? LimitValue { get; set; }
}

public sealed class SubscriptionIndexViewModel
{
    public string? Search { get; set; }
    public Guid? PlanId { get; set; }
    public string? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public PagedResult<SubscriptionListItemDto>? Result { get; set; }
    public IReadOnlyCollection<PlanListItemDto> Plans { get; set; } = [];
}

public sealed class AssignSubscriptionViewModel
{
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public bool StartTrial { get; set; }
    public IReadOnlyCollection<PlanListItemDto> Plans { get; set; } = [];
}
