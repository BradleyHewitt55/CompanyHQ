using System.Linq.Expressions;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Postgres.Services;

/// <summary>
/// Generic persistence for any <see cref="BaseModel"/>, in this provider's own terms: it hands
/// out an <see cref="IQueryable{T}"/> and throws EF Core's exceptions. Write methods save
/// immediately; inject <c>PostgresDbContext</c> directly when several models must commit in one
/// transaction.
/// </summary>
/// <remarks>
/// CompanyHQ's own services use the provider-neutral <c>IRepository</c> and <c>IPersistence</c>
/// instead. This stays for code that deliberately depends on PostgreSQL and EF Core.
/// </remarks>
public interface IPostgresService<TModel> where TModel : BaseModel
{
    /// <summary>Untracked queryable for reads and projections. Never use it to mutate.</summary>
    IQueryable<TModel> Query();

    Task<TModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TModel>> ListAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>One page, newest first. Use <see cref="Query"/> when the filter needs SQL the
    /// predicate cannot express.</summary>
    Task<PagedResult<TModel>> PageAsync(
        PageRequest page,
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>One page projected to <typeparamref name="TItem"/> in SQL.</summary>
    Task<PagedResult<TItem>> PageAsync<TItem>(
        PageRequest page,
        Expression<Func<TModel, TItem>> selector,
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts and saves. Assigns <see cref="BaseModel.Id"/> when it is unset.</summary>
    Task<TModel> AddAsync(TModel model, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TModel>> AddRangeAsync(
        IEnumerable<TModel> models,
        CancellationToken cancellationToken = default);

    Task<TModel> UpdateAsync(TModel model, CancellationToken cancellationToken = default);

    /// <summary>Returns false when no row matched, so callers can map that to a 404.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every row matching <paramref name="predicate"/> in one statement and returns how many
    /// went. The predicate is the authorisation: put the owner check in it, never in a prior read.
    /// </summary>
    Task<int> DeleteAsync(
        Expression<Func<TModel, bool>> predicate,
        CancellationToken cancellationToken = default);
}
