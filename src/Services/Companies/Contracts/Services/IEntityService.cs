using System.Linq.Expressions;
using Services.Companies.Contracts;

namespace Services.Companies.Contracts.Services;

public interface IEntityService<T> where T : class, IEntityRecord
{
    Task<IReadOnlyList<T>> Get(CancellationToken cancellationToken = default);
    Task<T?> GetById(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetBy(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> Save(T entity, CancellationToken cancellationToken = default);
    Task<bool> Delete(Guid id, CancellationToken cancellationToken = default);
}
