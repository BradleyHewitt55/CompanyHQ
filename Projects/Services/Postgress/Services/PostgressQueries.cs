using System.Linq.Expressions;
using Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using PostgressDbContext = CompanyHQ.Services.Postgress.DbContext;

namespace CompanyHQ.Services.Postgress.Services;

public sealed class PostgressQueries<TEntity>(PostgressDbContext dbContext) : IQueryService<TEntity>
    where TEntity : BaseModel
{
    private readonly PostgressDbContext _dbContext = dbContext;

    // Reads never track: the query service is read-only by contract, and
    // tracking here would silently interfere with repository writes in the same scope.
    private IQueryable<TEntity> Query(Expression<Func<TEntity, bool>>? predicate)
    {
        var query = _dbContext.Set<TEntity>().AsNoTracking();
        return predicate is null ? query : query.Where(predicate);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await Query(predicate).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => await Query(predicate).ToListAsync(cancellationToken);

    public async Task<PagedResult<TEntity>> PageAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        bool descending = false,
        CancellationToken cancellationToken = default)
    {
        var query = Query(predicate);
        var totalCount = await query.CountAsync(cancellationToken);

        // Paging without a stable order is nondeterministic in Postgres, so fall back to Id.
        var ordered = orderBy is null
            ? query.OrderBy(e => e.Id)
            : descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, page, pageSize, totalCount);
    }

    public async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => await Query(predicate).CountAsync(cancellationToken);
}
