using System.Linq.Expressions;
using CompanyHQ.Postgres.Contracts.Models;

namespace CompanyHQ.Persistence.Services;

/// <summary>
/// Persistence for one model. Write methods save immediately; use <see cref="IPersistence"/> when
/// several models must commit in one transaction.
/// </summary>
public interface IRepository<TModel> where TModel : BaseModel
{
    /// <summary>A read for filtering, paging and projections. Its results are snapshots; never use it to mutate.</summary>
    IQuery<TModel> Query();

    Task<TModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TModel>> ListAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts and saves. Assigns <see cref="BaseModel.Id"/> when it is unset.</summary>
    /// <exception cref="UniqueConstraintException">A unique key was already taken. The model is no longer staged, so the caller may retry.</exception>
    Task<TModel> AddAsync(TModel model, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="AddAsync"/>
    Task<IReadOnlyList<TModel>> AddRangeAsync(
        IEnumerable<TModel> models,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a model read earlier, whether or not it is still tracked.</summary>
    /// <exception cref="UniqueConstraintException">A unique key was already taken.</exception>
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
