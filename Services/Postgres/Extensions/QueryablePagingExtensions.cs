using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Postgres.Extensions;

/// <summary>
/// Paging over any <see cref="BaseModel"/> query, so controllers do not re-implement
/// count/skip/take and its ordering trap.
/// </summary>
public static class QueryablePagingExtensions
{
    /// <summary>Pages the query and projects each row in SQL, so entities are never materialised.</summary>
    public static async Task<PagedResult<TItem>> ToPagedResultAsync<TModel, TItem>(
        this IQueryable<TModel> query,
        PageRequest page,
        Expression<Func<TModel, TItem>> selector,
        Func<IQueryable<TModel>, IOrderedQueryable<TModel>>? orderBy = null,
        CancellationToken cancellationToken = default)
        where TModel : BaseModel
    {
        var total = await query.CountAsync(cancellationToken);

        var items = await Order(query, orderBy)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(selector)
            .ToListAsync(cancellationToken);

        return new PagedResult<TItem>(items, page.Page, page.PageSize, total);
    }

    /// <summary>Pages the query without projecting.</summary>
    public static async Task<PagedResult<TModel>> ToPagedResultAsync<TModel>(
        this IQueryable<TModel> query,
        PageRequest page,
        Func<IQueryable<TModel>, IOrderedQueryable<TModel>>? orderBy = null,
        CancellationToken cancellationToken = default)
        where TModel : BaseModel
    {
        var total = await query.CountAsync(cancellationToken);

        var items = await Order(query, orderBy)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TModel>(items, page.Page, page.PageSize, total);
    }

    /// <summary>
    /// Newest first, tie-broken by id. Without a total order Postgres may return rows in any order,
    /// which lets <c>Skip</c>/<c>Take</c> repeat a row on one page and drop it from the next.
    /// </summary>
    private static IOrderedQueryable<TModel> Order<TModel>(
        IQueryable<TModel> query,
        Func<IQueryable<TModel>, IOrderedQueryable<TModel>>? orderBy)
        where TModel : BaseModel =>
        orderBy is null
            ? query.OrderByDescending(model => model.CreatedAt).ThenBy(model => model.Id)
            : orderBy(query);
}
