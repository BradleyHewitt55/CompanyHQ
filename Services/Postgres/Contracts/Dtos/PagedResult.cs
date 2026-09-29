using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Postgres.Contracts.Dtos;

/// <summary>
/// One page of results plus the totals a client needs to render paging controls.
/// </summary>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResult<TItem> Empty(PageRequest request) =>
        new([], request.Page, request.PageSize, 0);
}
