using System.Linq.Expressions;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Moderation.Services;

internal static class Paging
{
    internal static async Task<PagedResult<TItem>> ToPagedResultAsync<T, TItem>(
        this IQuery<T> query, PageRequest paging, Expression<Func<T, TItem>> selector,
        Func<IQuery<T>, IOrderedQuery<T>> order, CancellationToken ct)
    {
        var page = await order(query).ToPageAsync(paging.Skip, paging.PageSize, selector, ct);
        return new(page.Items, paging.Page, paging.PageSize, page.Total);
    }
}
