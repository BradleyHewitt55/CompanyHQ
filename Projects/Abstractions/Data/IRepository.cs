namespace Abstractions.Data;

// CRUD only. Anything beyond a lookup by id belongs in IQueryService so the
// write path stays trivially swappable across storage providers.
public interface IRepository<TEntity> where TEntity : BaseModel
{
    Task<TEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
