using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Tenants.Models;

namespace OrizonAgents.Web.Models.Tenants;

public sealed class TenantFilterViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public string? Sort { get; set; }

    public int PageNumber { get; set; } = 1;

    public PagedResult<TenantListItemDto>? Result { get; set; }
}
