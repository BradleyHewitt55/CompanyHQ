namespace Abstractions.Data;

public sealed record PagedResult<TEntity>(
    IReadOnlyList<TEntity> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
