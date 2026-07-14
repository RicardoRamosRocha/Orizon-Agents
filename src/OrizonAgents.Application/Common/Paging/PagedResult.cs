namespace OrizonAgents.Application.Common.Paging;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => TotalItems == 0
        ? 0
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
